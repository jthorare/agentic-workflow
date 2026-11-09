// Browser entry: hydrate the server-rendered page from the #tdd-data island. On any failure the static page stays.
import { hydrateRoot } from "react-dom/client";
import { App } from "./App";
import type { Content } from "./types";

const root = document.getElementById("tdd-root");
const data = document.getElementById("tdd-data");
if (root && data) {
  document.documentElement.classList.add("js");
  try {
    const content = JSON.parse(data.textContent || "null") as Content;
    hydrateRoot(root, <App content={content} />, {
      onRecoverableError(error, info) {
        console.error("tdd: hydration recovered from an error (the server HTML did not match)", error, info?.componentStack || "");
      },
    });
  } catch (error) {
    console.error("tdd: hydration failed; the server-rendered page stays as it is", error);
  }
}
