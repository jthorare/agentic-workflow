import { createContext, useContext } from "react";
import type { Content } from "../types";
export const ContentCtx = createContext<Content | null>(null);
export const useContent = () => useContext(ContentCtx)!;
