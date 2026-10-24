#!/usr/bin/env python3
"""Drives the running Sindri app on an X display (Xvfb) with XTEST events and saves frames as PNG (whole-map proof,
docs/superpowers/evidence/whole-map-fixture/README.md). Not a test: needs an X server and the app running against the UI fixture.

  sindri-ui-capture.py DISPLAY WxH step...      steps, in order:
    resize          resize the app window to the screen
    shot:PATH       save the screen as PNG
    click:X,Y       move and click
    key:NAME        press a key (X keysym name: Escape, Return, g, c)
    type:TEXT       type text, one key per character
    sleep:SECONDS
    timed-click:X,Y:PATH   click, then poll frames until the screen stops changing for 1.2 s (below y=170, at most 6 s); print the time to the first
                           frame that differs from the one before the click and the time to the last change; save the last frame
"""
import sys, time
from Xlib import X, XK, display
from Xlib.ext import xtest
from PIL import Image, ImageChops

import os
MIN_WAIT = float(os.environ.get("CAP_MIN_WAIT", "0"))   # keep polling at least this long (a slow first load can stay blank for seconds)
disp, size, *steps = sys.argv[1:]
w, h = (int(v) for v in size.split("x"))
d = display.Display(disp)
root = d.screen().root


def app(win):
    for c in win.query_tree().children:
        if (c.get_wm_name() or "") == "Sindri":
            return c
        f = app(c)
        if f:
            return f


def frame():
    i = root.get_image(0, 0, w, h, X.ZPixmap, 0xFFFFFFFF)
    return Image.frombytes("RGB", (w, h), i.data, "raw", "BGRX")


def press(name, shift=False):
    code = d.keysym_to_keycode(XK.string_to_keysym(name))
    if shift:
        xtest.fake_input(d, X.KeyPress, d.keysym_to_keycode(XK.string_to_keysym("Shift_L")))
    xtest.fake_input(d, X.KeyPress, code)
    xtest.fake_input(d, X.KeyRelease, code)
    if shift:
        xtest.fake_input(d, X.KeyRelease, d.keysym_to_keycode(XK.string_to_keysym("Shift_L")))
    d.sync()
    time.sleep(0.05)


def click(x, y):
    xtest.fake_input(d, X.MotionNotify, x=x, y=y)
    xtest.fake_input(d, X.ButtonPress, 1)
    xtest.fake_input(d, X.ButtonRelease, 1)
    d.sync()


for s in steps:
    op, _, arg = s.partition(":")
    if op == "resize":
        a = app(root)
        a.configure(x=0, y=0, width=w, height=h)
        d.sync()
        time.sleep(1.5)
    elif op == "shot":
        frame().save(arg)
    elif op == "click":
        click(*(int(v) for v in arg.split(",")))
    elif op == "key":
        press(arg)
    elif op == "type":
        for ch in arg:
            press("space" if ch == " " else ch)
    elif op == "sleep":
        time.sleep(float(arg))
    elif op == "timed-click":
        xy, _, path = arg.partition(":")
        before = frame()
        t0 = time.time()
        click(*(int(v) for v in xy.split(",")))
        first = last = None
        prev = before
        box = (0, 170, w, h)   # below the header: the tick of "updated Ns ago" and the caret stay out of the measure
        while time.time() - t0 < max(6, MIN_WAIT):
            cur = frame()
            now = time.time() - t0
            if ImageChops.difference(prev.crop(box), cur.crop(box)).getbbox():
                last = now
                if first is None and ImageChops.difference(before.crop(box), cur.crop(box)).getbbox():
                    first = now
                prev = cur
            elif last is not None and now - last > 1.2 and now > MIN_WAIT:
                break
        print("first-changed-frame %.3fs, settled %.3fs" % (first or -1, last or -1))
        prev.save(path)
    else:
        sys.exit("unknown step " + s)
