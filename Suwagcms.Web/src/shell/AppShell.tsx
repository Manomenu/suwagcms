import { useState, type ReactNode } from "react";
import { Link, useLocation } from "wouter";

import { cn } from "@/design/classNames";
import { mayLeave } from "@/api/leaveGuard";

import { AppNavbar } from "./AppNavbar";
import { CONTENT_MAX_WIDTH, HEADER_HEIGHT, LOGO_SIZE, PANEL_SIDE, PANEL_WIDTH, panelSideClasses } from "./layout";
import { navigationOf } from "./navigation";
import { useBots } from "./useBots";

export function AppShell({ children }: { children: ReactNode }) {
    const [pathname] = useLocation();
    const { bots, failure } = useBots(pathname);
    const { bot, tab, fills } = navigationOf(pathname);
    const [opened, setOpened] = useState(false);
    const side = panelSideClasses(PANEL_SIDE);

    return (
        <div className="min-h-screen">
            <header
                className="sticky top-0 z-30 flex items-center gap-3 border-b border-border bg-background px-4"
                style={{ height: HEADER_HEIGHT }}
            >
                <button
                    type="button"
                    aria-label="Menu"
                    aria-expanded={opened}
                    className="rounded-md px-2 py-1 text-xl leading-none hover:bg-accent/50 md:hidden"
                    onClick={() => {
                        setOpened(!opened);
                    }}
                >
                    <svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true" stroke="currentColor" strokeWidth="2">
                        <path d="M2 5h16M2 10h16M2 15h16" />
                    </svg>
                </button>
                <Link
                    href="/"
                    className="flex items-center gap-2 text-foreground"
                    onClick={(event) => {
                        if (!mayLeave()) event.preventDefault();
                        else setOpened(false);
                    }}
                >
                    <img src="/favicon.svg" alt="" width={LOGO_SIZE} height={LOGO_SIZE} />
                    <span className="text-lg font-bold">suwagcms</span>
                </Link>
            </header>
            <div className={cn("flex", side.row)}>
                <aside
                    className={cn(
                        "shrink-0 overflow-y-auto border-border bg-background p-2.5 md:sticky md:block",
                        side.border,
                        opened ? "max-md:fixed max-md:z-20" : "hidden",
                        side.overlay,
                    )}
                    style={{ width: PANEL_WIDTH, top: HEADER_HEIGHT, height: `calc(100vh - ${HEADER_HEIGHT}px)` }}
                >
                    <AppNavbar
                        bots={bots}
                        failure={failure}
                        bot={bot}
                        tab={tab}
                        onNavigate={() => {
                            setOpened(false);
                        }}
                    />
                </aside>
                <main
                    className={cn("min-w-0 flex-1", !fills && "p-4", fills && "overflow-hidden")}
                    style={fills ? { height: `calc(100vh - ${HEADER_HEIGHT}px)` } : undefined}
                >
                    {fills ? <div className="h-full">{children}</div> : <div style={{ maxWidth: CONTENT_MAX_WIDTH }}>{children}</div>}
                </main>
            </div>
        </div>
    );
}
