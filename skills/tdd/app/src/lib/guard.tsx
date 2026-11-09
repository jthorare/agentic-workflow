import { Component, type ReactNode } from "react";
/** Keeps an interactive island's failure from unmounting the document: on a render error it renders nothing. */
export class Guard extends Component<{ name: string; children: ReactNode }, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() { return { failed: true }; }
  componentDidCatch(error: unknown) { console.error(`tdd: ${this.props.name} failed and was switched off`, error); }
  render() { return this.state.failed ? null : this.props.children; }
}
