// Tiles on-screen Warp windows into equal sections of the display under the pointer.
//
//   Control-Option-1 ... Control-Option-9  -> that many sections
//   Control-Option-0                       -> 10 sections
//
// Two processes share this binary. The key listener is a normal app so macOS delivers
// the shortcut. The mover is started with --worker from a Warp or Cursor terminal so it
// inherits that app's Accessibility access. A login item does not receive the grant.
// The listener forwards the count over /tmp/warp-screen-layout.sock. The mover also
// watches the keyboard directly.
//
// Sections are full-height columns. When a column would be narrower than
// `minimumColumnWidth`, the sections wrap into two rows that still fill the screen.
//
// Install: scripts/warp-screen-layout/install.sh

import AppKit
import Carbon
import CoreGraphics
import Darwin
import Foundation

let layoutSocketPath = "/tmp/warp-screen-layout.sock"
let layoutLogPath = NSHomeDirectory() + "/Library/Logs/warp-screen-layout.log"

func logLine(_ message: String) {
  let line = message + "\n"
  print(line, terminator: "")
  fflush(stdout)
  let url = URL(fileURLWithPath: layoutLogPath)
  guard let data = line.data(using: .utf8) else { return }
  if FileManager.default.fileExists(atPath: layoutLogPath), let handle = FileHandle(forWritingAtPath: layoutLogPath) {
    handle.seekToEndOfFile()
    handle.write(data)
    try? handle.close()
  } else {
    try? data.write(to: url)
  }
}

/// Columns narrower than this wrap onto a second row. 800px keeps a terminal usable on the 5120-wide display.
let minimumColumnWidth: CGFloat = 800
let warpBundlePrefix = "dev.warp.Warp"

@_silgen_name("_AXUIElementGetWindow")
func copyAXWindowID(_ element: AXUIElement, _ identifier: UnsafeMutablePointer<CGWindowID>) -> AXError

struct PlacedWindow {
  let element: AXUIElement
  let title: String
  let frame: CGRect
  let zOrder: Int
}

func fourCharCode(_ string: String) -> OSType {
  string.utf8.reduce(0) { ($0 << 8) + OSType($1) }
}

func axRect(from frame: CGRect, on screen: NSScreen) -> CGRect {
  let primary = NSScreen.screens.first { $0.frame.origin == .zero } ?? screen
  return CGRect(
    x: frame.origin.x,
    y: primary.frame.height - frame.origin.y - frame.height,
    width: frame.width,
    height: frame.height
  )
}

func cocoaRect(from axFrame: CGRect) -> CGRect {
  let primary = NSScreen.screens.first { $0.frame.origin == .zero } ?? NSScreen.screens[0]
  return CGRect(
    x: axFrame.origin.x,
    y: primary.frame.height - axFrame.origin.y - axFrame.height,
    width: axFrame.width,
    height: axFrame.height
  )
}

/// Equal sections that fill `screen.visibleFrame`, in AX coordinates (top-left origin).
func sectionFrames(count: Int, on screen: NSScreen) -> [CGRect] {
  let visible = screen.visibleFrame
  let totalWidth = Int(visible.width.rounded())
  let totalHeight = Int(visible.height.rounded())
  let maxColumns = max(1, Int(floor(visible.width / minimumColumnWidth)))
  let rows = min(2, max(1, Int(ceil(Double(count) / Double(maxColumns)))))
  var rowCounts = Array(repeating: count / rows, count: rows)
  var remainder = count % rows
  for index in rowCounts.indices where remainder > 0 {
    rowCounts[index] += 1
    remainder -= 1
  }

  let baseRowHeight = totalHeight / rows
  let extraRowPixels = totalHeight % rows
  var frames: [CGRect] = []
  var pixelsFromTop = 0

  for row in 0..<rows {
    let rowHeight = baseRowHeight + (row < extraRowPixels ? 1 : 0)
    let columns = rowCounts[row]
    let baseColumnWidth = totalWidth / columns
    let extraColumnPixels = totalWidth % columns
    var pixelsFromLeft = 0
    let cocoaTop = visible.maxY - CGFloat(pixelsFromTop)

    for column in 0..<columns {
      let columnWidth = baseColumnWidth + (column < extraColumnPixels ? 1 : 0)
      let cocoaFrame = CGRect(
        x: visible.minX + CGFloat(pixelsFromLeft),
        y: cocoaTop - CGFloat(rowHeight),
        width: CGFloat(columnWidth),
        height: CGFloat(rowHeight)
      )
      frames.append(axRect(from: cocoaFrame, on: screen))
      pixelsFromLeft += columnWidth
    }
    pixelsFromTop += rowHeight
  }
  return frames
}

func stringAttribute(_ element: AXUIElement, _ name: String) -> String? {
  var value: CFTypeRef?
  guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else { return nil }
  return value as? String
}

func boolAttribute(_ element: AXUIElement, _ name: String) -> Bool? {
  var value: CFTypeRef?
  guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else { return nil }
  return value as? Bool
}

func pointAttribute(_ element: AXUIElement, _ name: String) -> CGPoint? {
  var value: CFTypeRef?
  guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else { return nil }
  guard let axValue = value, CFGetTypeID(axValue) == AXValueGetTypeID() else { return nil }
  var point = CGPoint.zero
  guard AXValueGetValue(axValue as! AXValue, .cgPoint, &point) else { return nil }
  return point
}

func sizeAttribute(_ element: AXUIElement, _ name: String) -> CGSize? {
  var value: CFTypeRef?
  guard AXUIElementCopyAttributeValue(element, name as CFString, &value) == .success else { return nil }
  guard let axValue = value, CFGetTypeID(axValue) == AXValueGetTypeID() else { return nil }
  var size = CGSize.zero
  guard AXValueGetValue(axValue as! AXValue, .cgSize, &size) else { return nil }
  return size
}

func frameAttribute(_ element: AXUIElement) -> CGRect? {
  guard let origin = pointAttribute(element, kAXPositionAttribute),
        let size = sizeAttribute(element, kAXSizeAttribute) else { return nil }
  return CGRect(origin: origin, size: size)
}

func setFrame(_ element: AXUIElement, _ frame: CGRect) -> AXError {
  var origin = frame.origin
  var size = frame.size
  guard let originValue = AXValueCreate(.cgPoint, &origin),
        let sizeValue = AXValueCreate(.cgSize, &size) else { return .failure }
  _ = AXUIElementSetAttributeValue(element, kAXSizeAttribute as CFString, sizeValue)
  let originError = AXUIElementSetAttributeValue(element, kAXPositionAttribute as CFString, originValue)
  // A second size write wins when the window manager clamped the first one against the old origin.
  let sizeError = AXUIElementSetAttributeValue(element, kAXSizeAttribute as CFString, sizeValue)
  if originError != .success { return originError }
  return sizeError
}

func warpApplications() -> [NSRunningApplication] {
  NSWorkspace.shared.runningApplications.filter { app in
    if app.localizedName == "Warp" { return true }
    return app.bundleIdentifier?.hasPrefix(warpBundlePrefix) == true
  }
}

func zOrderByWindowID() -> [CGWindowID: Int] {
  guard let info = CGWindowListCopyWindowInfo([.optionOnScreenOnly, .excludeDesktopElements], kCGNullWindowID) as? [[String: Any]] else {
    return [:]
  }
  var order: [CGWindowID: Int] = [:]
  for (index, window) in info.enumerated() {
    guard let id = window[kCGWindowNumber as String] as? CGWindowID else { continue }
    order[id] = index
  }
  return order
}

func windows(on screen: NSScreen) -> [PlacedWindow] {
  let order = zOrderByWindowID()
  var placed: [PlacedWindow] = []
  for app in warpApplications() {
    let element = AXUIElementCreateApplication(app.processIdentifier)
    AXUIElementSetMessagingTimeout(element, 1)
    var value: CFTypeRef?
    guard AXUIElementCopyAttributeValue(element, kAXWindowsAttribute as CFString, &value) == .success,
          let windows = value as? [AXUIElement] else { continue }
    for window in windows {
      if boolAttribute(window, kAXMinimizedAttribute) == true { continue }
      if let role = stringAttribute(window, kAXRoleAttribute), role != kAXWindowRole { continue }
      guard let frame = frameAttribute(window), frame.width >= 200, frame.height >= 200 else { continue }
      let center = CGPoint(x: cocoaRect(from: frame).midX, y: cocoaRect(from: frame).midY)
      guard screen.frame.contains(center) else { continue }
      var windowID: CGWindowID = 0
      let zOrder = copyAXWindowID(window, &windowID) == .success ? (order[windowID] ?? Int.max) : Int.max
      placed.append(PlacedWindow(
        element: window,
        title: stringAttribute(window, kAXTitleAttribute) ?? "Warp",
        frame: frame,
        zOrder: zOrder
      ))
    }
  }
  return placed
}

func windowsToPlace(_ windows: [PlacedWindow], sections: Int) -> [PlacedWindow] {
  let visible = windows.sorted { lhs, rhs in
    if lhs.zOrder != rhs.zOrder { return lhs.zOrder < rhs.zOrder }
    return lhs.frame.minX < rhs.frame.minX
  }
  let chosen = Array(visible.prefix(sections))
  return chosen.sorted { lhs, rhs in
    if abs(lhs.frame.minX - rhs.frame.minX) > 8 { return lhs.frame.minX < rhs.frame.minX }
    return lhs.frame.minY < rhs.frame.minY
  }
}

func screenUnderPointer() -> NSScreen? {
  let point = NSEvent.mouseLocation
  return NSScreen.screens.first { $0.frame.contains(point) } ?? NSScreen.main
}

func describe(_ frame: CGRect) -> String {
  "x=\(Int(frame.minX)) y=\(Int(frame.minY)) \(Int(frame.width))x\(Int(frame.height))"
}

var permissionPrompt: PermissionPrompt?
var permissionPromptShown = false

/// Shows the Accessibility window once per run. The window is built here, after NSApplication exists, and never in --print mode.
func showPermissionPromptOnce() {
  if permissionPromptShown { return }
  permissionPromptShown = true
  let prompt = PermissionPrompt()
  permissionPrompt = prompt
  prompt.show()
}

func tile(sections: Int, apply: Bool) {
  guard AXIsProcessTrusted() else {
    fputs("Accessibility permission is off. Enable Warp Screen Layout in System Settings > Privacy & Security > Accessibility.\n", stderr)
    showPermissionPromptOnce()
    return
  }
  guard let screen = screenUnderPointer() else {
    fputs("No display under the pointer.\n", stderr)
    return
  }
  let frames = sectionFrames(count: sections, on: screen)
  let all = windows(on: screen)
  let chosen = windowsToPlace(all, sections: sections)
  let timestamp = ISO8601DateFormatter().string(from: Date())
  print("\(timestamp) \(apply ? "apply" : "plan") \(sections) sections, \(chosen.count) of \(all.count) Warp windows")
  for (index, window) in chosen.enumerated() {
    let slot = frames[index]
    print("  [\(index)] \(describe(window.frame)) -> \(describe(slot))  \(window.title)")
    if apply {
      if boolAttribute(window.element, "AXFullScreen") == true {
        AXUIElementSetAttributeValue(window.element, "AXFullScreen" as CFString, kCFBooleanFalse)
      }
      let error = setFrame(window.element, slot)
      if error != .success {
        fputs("  failed to move \"\(window.title)\": \(error.rawValue)\n", stderr)
      }
    }
  }
  if all.count > chosen.count {
    print("  left \(all.count - chosen.count) Warp windows where they were")
  }
  fflush(stdout)
}

func printSections(count: Int) {
  guard let screen = NSScreen.main else { return }
  let frames = sectionFrames(count: count, on: screen)
  print("\(count) sections on \(Int(screen.visibleFrame.width))x\(Int(screen.visibleFrame.height))")
  for (index, frame) in frames.enumerated() {
    print("  [\(index)] \(describe(frame))")
  }
  let widthSum = frames.reduce(0) { $0 + Int($1.width) }
  let rowCount = Set(frames.map { Int($0.minY) }).count
  let widthPerRow = widthSum / rowCount
  let expected = Int(screen.visibleFrame.width.rounded())
  if widthPerRow != expected {
    fputs("width sum \(widthPerRow) != \(expected)\n", stderr)
    exit(1)
  }
}

func setSocketPath(_ address: inout sockaddr_un, _ path: String) {
  path.withCString { cString in
    withUnsafeMutablePointer(to: &address.sun_path) { pointer in
      pointer.withMemoryRebound(to: CChar.self, capacity: 104) { destination in
        strncpy(destination, cString, 103)
        destination[103] = 0
      }
    }
  }
}

func withSocketAddress<T>(_ path: String, _ body: (UnsafePointer<sockaddr>, socklen_t) -> T) -> T {
  var address = sockaddr_un()
  address.sun_family = sa_family_t(AF_UNIX)
  setSocketPath(&address, path)
  let length = socklen_t(MemoryLayout<sockaddr_un>.size)
  return withUnsafePointer(to: &address) { pointer in
    pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { body($0, length) }
  }
}

func forward(sections: Int) -> Bool {
  let fd = socket(AF_UNIX, SOCK_STREAM, 0)
  if fd < 0 { return false }
  defer { close(fd) }
  let connected = withSocketAddress(layoutSocketPath) { connect(fd, $0, $1) }
  if connected != 0 { return false }
  var byte = UInt8(sections)
  return write(fd, &byte, 1) == 1
}

func sections(for keyCode: Int64) -> Int? {
  switch Int(keyCode) {
  case kVK_ANSI_1, kVK_ANSI_Keypad1: 1
  case kVK_ANSI_2, kVK_ANSI_Keypad2: 2
  case kVK_ANSI_3, kVK_ANSI_Keypad3: 3
  case kVK_ANSI_4, kVK_ANSI_Keypad4: 4
  case kVK_ANSI_5, kVK_ANSI_Keypad5: 5
  case kVK_ANSI_6, kVK_ANSI_Keypad6: 6
  case kVK_ANSI_7, kVK_ANSI_Keypad7: 7
  case kVK_ANSI_8, kVK_ANSI_Keypad8: 8
  case kVK_ANSI_9, kVK_ANSI_Keypad9: 9
  case kVK_ANSI_0, kVK_ANSI_Keypad0: 10
  default: nil
  }
}

var retainedKeyTap: CFMachPort?
var retainedKeyTapSource: CFRunLoopSource?

func installKeyTap() {
  let mask = CGEventMask(1 << CGEventType.keyDown.rawValue)
  guard let tap = CGEvent.tapCreate(
    tap: .cgSessionEventTap,
    place: .headInsertEventTap,
    options: .defaultTap,
    eventsOfInterest: mask,
    callback: { _, type, event, _ in
      if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
        if let tap = retainedKeyTap { CGEvent.tapEnable(tap: tap, enable: true) }
        return Unmanaged.passUnretained(event)
      }
      let flags = event.flags
      guard flags.contains(.maskControl), flags.contains(.maskAlternate), !flags.contains(.maskCommand) else {
        return Unmanaged.passUnretained(event)
      }
      let code = event.getIntegerValueField(.keyboardEventKeycode)
      guard let sectionCount = sections(for: code) else {
        logLine("ignored control-option key code \(code)")
        return Unmanaged.passUnretained(event)
      }
      DispatchQueue.main.async {
        logLine("key tap \(sectionCount)")
        tile(sections: sectionCount, apply: true)
      }
      return nil
    },
    userInfo: nil
  ) else {
    logLine("key tap was not created")
    return
  }
  retainedKeyTap = tap
  let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
  retainedKeyTapSource = source
  CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
  CGEvent.tapEnable(tap: tap, enable: true)
  logLine("key tap installed")
}

func runWorker() {
  unlink(layoutSocketPath)
  let fd = socket(AF_UNIX, SOCK_STREAM, 0)
  guard fd >= 0 else {
    logLine("worker socket failed")
    exit(1)
  }
  let bound = withSocketAddress(layoutSocketPath) { bind(fd, $0, $1) }
  guard bound == 0, listen(fd, 8) == 0 else {
    logLine("worker bind failed")
    exit(1)
  }
  logLine("worker listening (accessibility: \(AXIsProcessTrusted()))")
  installKeyTap()
  if retainedKeyTap == nil {
    // No grant yet, so the tap could not be created. Retry every 5 seconds until Accessibility is on.
    Timer.scheduledTimer(withTimeInterval: 5, repeats: true) { timer in
      guard AXIsProcessTrusted() else { return }
      installKeyTap()
      if retainedKeyTap != nil {
        logLine("accessibility granted, key tap recovered")
        timer.invalidate()
      }
    }
  }
  DispatchQueue.global(qos: .userInitiated).async {
    while true {
      let client = accept(fd, nil, nil)
      if client < 0 { continue }
      var byte: UInt8 = 0
      let count = read(client, &byte, 1)
      close(client)
      let sections = Int(byte)
      guard count == 1, (1...10).contains(sections) else { continue }
      DispatchQueue.main.async {
        logLine("worker tiling \(sections)")
        tile(sections: sections, apply: true)
      }
    }
  }
  NSApplication.shared.run()
}

let hotkeyCallback: EventHandlerUPP = { _, event, _ in
  guard let event else { return OSStatus(eventNotHandledErr) }
  var hotKeyID = EventHotKeyID()
  let status = GetEventParameter(
    event,
    EventParamName(kEventParamDirectObject),
    EventParamType(typeEventHotKeyID),
    nil,
    MemoryLayout<EventHotKeyID>.size,
    nil,
    &hotKeyID
  )
  guard status == noErr else { return status }
  let sections = Int(hotKeyID.id)
  logLine("hotkey \(sections)")
  if forward(sections: sections) {
    logLine("forwarded \(sections)")
  } else if AXIsProcessTrusted() {
    tile(sections: sections, apply: true)
  } else {
    logLine("hotkey \(sections) could not move windows")
    showPermissionPromptOnce()
  }
  return noErr
}

var retainedHotKeys: [EventHotKeyRef] = []

func registerHotKeys() {
  var eventSpec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
  InstallEventHandler(GetApplicationEventTarget(), hotkeyCallback, 1, &eventSpec, nil, nil)

  let keyCodes = [
    1: kVK_ANSI_1, 2: kVK_ANSI_2, 3: kVK_ANSI_3, 4: kVK_ANSI_4, 5: kVK_ANSI_5,
    6: kVK_ANSI_6, 7: kVK_ANSI_7, 8: kVK_ANSI_8, 9: kVK_ANSI_9, 10: kVK_ANSI_0,
  ]
  let modifiers = UInt32(controlKey | optionKey)
  for (sections, keyCode) in keyCodes.sorted(by: { $0.key < $1.key }) {
    var ref: EventHotKeyRef?
    let status = RegisterEventHotKey(
      UInt32(keyCode),
      modifiers,
      EventHotKeyID(signature: fourCharCode("WSLA"), id: UInt32(sections)),
      GetApplicationEventTarget(),
      0,
      &ref
    )
    if status != noErr {
      fputs("Could not register Control-Option-\(sections == 10 ? "0" : String(sections)): \(status)\n", stderr)
    } else if let ref {
      retainedHotKeys.append(ref)
    }
  }
}

func promptForAccessibility() {
  let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
  let options = [key: true] as CFDictionary
  _ = AXIsProcessTrustedWithOptions(options)
}

final class PermissionPrompt: NSObject {
  let window: NSWindow
  var timer: Timer?

  override init() {
    window = NSWindow(
      contentRect: NSRect(x: 0, y: 0, width: 560, height: 250),
      styleMask: [.titled, .closable],
      backing: .buffered,
      defer: false
    )
    super.init()
    window.title = "Warp Screen Layout"
    window.level = .floating
    window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
    window.isReleasedWhenClosed = false

    let text = NSTextField(wrappingLabelWithString: """
    The Control-Option shortcuts are installed, and macOS is blocking it from moving windows.

    Click Open Settings. In the Accessibility list, turn on Warp Screen Layout.
    If it is not listed, click +, then choose Warp Screen Layout in Applications.

    Press Control-Option and a number again after the switch is on. This window closes itself once macOS allows it.
    """)
    text.frame = NSRect(x: 20, y: 64, width: 520, height: 160)
    window.contentView?.addSubview(text)

    let settings = NSButton(title: "Open Accessibility Settings", target: self, action: #selector(openSettings))
    settings.frame = NSRect(x: 20, y: 16, width: 230, height: 32)
    settings.bezelStyle = .rounded
    window.contentView?.addSubview(settings)

    let reveal = NSButton(title: "Show the App in Finder", target: self, action: #selector(revealApp))
    reveal.frame = NSRect(x: 260, y: 16, width: 190, height: 32)
    reveal.bezelStyle = .rounded
    window.contentView?.addSubview(reveal)
  }

  func show() {
    NSApp.setActivationPolicy(.regular)
    NSApp.activate(ignoringOtherApps: true)
    window.center()
    window.makeKeyAndOrderFront(nil)
    promptForAccessibility()
    openSettings()
    timer = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] timer in
      guard AXIsProcessTrusted() else { return }
      print("accessibility granted")
      fflush(stdout)
      self?.window.orderOut(nil)
      NSApp.setActivationPolicy(.accessory)
      timer.invalidate()
    }
  }

  @objc func openSettings() {
    guard let url = URL(string: "x-apple.systempreferences:com.apple.settings.PrivacySecurity.extension?Privacy_Accessibility") else { return }
    NSWorkspace.shared.open(url)
  }

  @objc func revealApp() {
    let appURL = Bundle.main.bundleURL
    NSWorkspace.shared.activateFileViewerSelecting([appURL])
  }
}

let arguments = CommandLine.arguments
NSApplication.shared.setActivationPolicy(.accessory)

if let printFlag = arguments.firstIndex(of: "--print"), printFlag + 1 < arguments.count, let count = Int(arguments[printFlag + 1]) {
  printSections(count: count)
  exit(0)
}

if let planFlag = arguments.firstIndex(of: "--plan"), planFlag + 1 < arguments.count, let count = Int(arguments[planFlag + 1]) {
  promptForAccessibility()
  tile(sections: count, apply: false)
  exit(0)
}

if arguments.contains("--worker") {
  runWorker()
  exit(0)
}

registerHotKeys()
logLine("hotkeys listening (accessibility: \(AXIsProcessTrusted()))")
NSApplication.shared.run()
