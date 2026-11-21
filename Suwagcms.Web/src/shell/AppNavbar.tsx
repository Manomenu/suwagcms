import type { ReactElement } from "react";
import { NavTree, Spinner, type NavItem1 } from "@maniumek/suwfrontkit";
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

function treeOf(bots: Bot[] | null, bot: string | null, tab: Tab | null): NavItem1[] {
    return [
        {
            icon: <BotMessageSquare />,
            label: "Bots",
            href: "/bots",
            current: bot === null,
            items: (bots ?? []).map((entry) => {
                const isOpen = entry.id === bot;
                return {
                    icon: <BotIcon />,
                    label: entry.name,
                    href: botHref(entry.id),
                    title: entry.address,
                    current: isOpen && tab === null,
                    ...(isOpen
                        ? {
                              items: TABS.map((item) => ({
                                  icon: TAB_ICONS[item.id],
                                  label: item.label,
                                  href: tabHref(entry.id, item.id),
                                  current: tab === item.id,
                              })),
                          }
                        : {}),
                };
            }),
        },
    ];
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
            <NavTree items={treeOf(bots, bot, tab)} />
            {bots === null && failure === null && <Spinner label="Loading bots…" size="sm" className="px-2 py-2 text-sm" />}
            {failure !== null && <p className="px-2 py-2 text-xs text-muted-foreground">{failure}</p>}
        </nav>
    );
}
