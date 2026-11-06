import { useEffect } from "react";

let question: string | null = null;

export function useLeaveGuard(active: boolean, message: string) {
    useEffect(() => {
        if (!active) return;
        // Why: the browser only shows its own text, so the listener just has to cancel the unload.
        const warn = (event: BeforeUnloadEvent) => event.preventDefault();
        question = message;
        window.addEventListener("beforeunload", warn);
        return () => {
            question = null;
            window.removeEventListener("beforeunload", warn);
        };
    }, [active, message]);
}

export function mayLeave(): boolean {
    return question === null || window.confirm(question);
}

export const DISCARD_QUESTION = "Discard the changes you have not saved?";
