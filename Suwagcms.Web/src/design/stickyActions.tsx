import type { ReactNode } from "react";

/**
 * The actions at the end of a resource's form. They stay in view while a long form scrolls, and at the end of the form they rest in
 * their own place, so they cover neither its last row nor what follows it.
 */
export function StickyActions({ children }: { children: ReactNode }) {
    return <div className="sticky bottom-0 z-10 flex items-center gap-2 border-t border-border bg-card py-2">{children}</div>;
}
