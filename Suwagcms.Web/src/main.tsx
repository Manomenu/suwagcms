import "./index.css";

import { StrictMode } from "react";
import { createRoot } from "react-dom/client";

import { App } from "./App";
import { ErrorBoundary } from "./shell/ErrorBoundary";

const root = document.getElementById("root");
if (!root) throw new Error("index.html has no #root element");

createRoot(root, {
    onUncaughtError: (error, info) => {
        console.error("Uncaught render error", error, info.componentStack);
    },
    onCaughtError: (error, info) => {
        console.error("Render error caught by an error boundary", error, info.componentStack);
    },
}).render(
    <StrictMode>
        <ErrorBoundary>
            <App />
        </ErrorBoundary>
    </StrictMode>,
);
