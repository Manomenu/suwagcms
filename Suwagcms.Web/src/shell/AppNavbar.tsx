import type { ReactElement } from "react";
import { mayLeave, NavLink } from "@maniumek/suwfrontkit";
import {
    Bot as BotIcon,
    BotMessageSquare,
    FileText,
    GitBranch,
    Library,
    ScrollText,
    UserRound,
    Variable,
    Workflow,
    Zap,
} from "lucide-react";
import { Link } from "wouter";

import type { Bot } from "@/api/bots";

import { botHref, TABS, tabHref, type Tab } from "./navigation";

const TAB_ICONS: Record<Tab, ReactElement> = {
    facts: <Variable />,
    "knowledge-bases": <Library />,
    articles: <FileText />,
    procedures: <Workflow />,
    actions: <Zap />,
    persona: <UserRound />,
    versions: <GitBranch />,
    history: <ScrollText />,
};

function guard(event: React.MouseEvent) {
    if (!mayLeave()) event.preventDefault();
}

export function AppNavbar({
    bots,
    failure,
    bot,
    tab,
}: {
    bots: Bot[] | null;
    failure: string | null;
    bot: string | null;
    tab: Tab | null;
}) {
    return (
        <nav aria-label="Bots" className="flex flex-col gap-0.5">
            <NavLink asChild icon={<BotMessageSquare />} label="Bots" current={bot === null}>
                <Link href="/bots" onClick={guard} />
            </NavLink>
            {bots === null && failure === null && <p className="px-2 py-2 text-sm text-muted-foreground">Loading bots…</p>}
            {failure !== null && <p className="px-2 py-2 text-xs text-muted-foreground">{failure}</p>}
            {bots !== null && bots.length > 0 && (
                <ul className="flex flex-col gap-0.5">
                    {bots.map((entry) => {
                        const isOpen = entry.id === bot;
                        const profile = isOpen && tab === null;
                        return (
                            <li key={entry.id} aria-current={isOpen && !profile ? "true" : undefined} className="flex flex-col gap-0.5">
                                <NavLink asChild icon={<BotIcon />} label={entry.name} current={profile}>
                                    <Link href={botHref(entry.id)} title={entry.address} onClick={guard} />
                                </NavLink>
                                {isOpen && (
                                    <ul className="flex flex-col gap-0.5">
                                        {TABS.map((item) => (
                                            <li key={item.id}>
                                                <NavLink asChild icon={TAB_ICONS[item.id]} label={item.label} current={tab === item.id}>
                                                    <Link href={tabHref(entry.id, item.id)} onClick={guard} />
                                                </NavLink>
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
