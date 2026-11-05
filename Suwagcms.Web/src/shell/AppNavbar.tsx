import { Link } from "wouter";

import type { Bot } from "@/api/bots";
import { cn } from "@/design/classNames";
import { mayLeave } from "@/design/leaveGuard";

import { botHref, TABS, tabHref, type Tab } from "./navigation";

const ITEM = "block rounded-md px-3 py-2 text-sm text-foreground hover:bg-accent/50";
const ACTIVE = "bg-primary-light text-primary-light-foreground";

export function AppNavbar({
    bots,
    failure,
    bot,
    tab,
    onNavigate,
}: {
    bots: Bot[] | null;
    failure: string | null;
    bot: string | null;
    tab: Tab | null;
    onNavigate?: () => void;
}) {
    function guard(event: React.MouseEvent) {
        if (!mayLeave()) event.preventDefault();
        else onNavigate?.();
    }

    return (
        <nav aria-label="Bots" className="flex flex-col gap-0.5">
            <Link
                href="/bots"
                onClick={guard}
                aria-current={bot === null ? "page" : undefined}
                className={cn(ITEM, bot === null && ACTIVE)}
            >
                Bots
            </Link>
            {bots === null && failure === null && <p className="px-3 py-2 text-sm text-muted-foreground">Loading bots…</p>}
            {failure !== null && <p className="px-3 py-2 text-xs text-muted-foreground">{failure}</p>}
            {bots !== null && bots.length > 0 && (
                <ul className="ml-4 flex flex-col gap-0.5">
                    {bots.map((entry) => {
                        const isOpen = entry.id === bot;
                        const profile = isOpen && tab === null;
                        return (
                            <li key={entry.id} aria-current={profile ? "page" : isOpen ? "true" : undefined}>
                                <Link
                                    href={botHref(entry.id)}
                                    title={entry.address}
                                    onClick={guard}
                                    className={cn(ITEM, profile && ACTIVE)}
                                >
                                    {entry.name}
                                </Link>
                                {isOpen && (
                                    <ul className="ml-4 flex flex-col gap-0.5">
                                        {TABS.map((item) => (
                                            <li key={item.id}>
                                                <Link
                                                    href={tabHref(entry.id, item.id)}
                                                    onClick={guard}
                                                    aria-current={tab === item.id ? "page" : undefined}
                                                    className={cn(ITEM, tab === item.id && ACTIVE)}
                                                >
                                                    {item.label}
                                                </Link>
                                            </li>
                                        ))}
                                    </ul>
                                )}
                            </li>
                        );
                    })}
                </ul>
            )}
        </nav>
    );
}
