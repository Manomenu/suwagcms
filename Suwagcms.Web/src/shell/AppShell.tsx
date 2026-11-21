import type { ReactNode } from "react";
import { AppShell as Frame, GuardedLink } from "@maniumek/suwfrontkit";
import { useLocation } from "wouter";

import { useBots } from "@/api/bots";

import { AppNavbar } from "./AppNavbar";
import { navigationOf } from "./navigation";

export function AppShell({ children }: { children: ReactNode }) {
    const [pathname] = useLocation();
    const { bots, failure } = useBots();
    const { bot, tab } = navigationOf(pathname);

    return (
        <Frame
            appIcon={<img src="/favicon.svg" alt="" />}
            appTitle="suwagcms"
            home={(link) => <GuardedLink href="/" {...link} />}
            panel={<AppNavbar bots={bots} failure={failure} bot={bot} tab={tab} />}
            panelLabel="Navigation panel"
            storageKey="suwagcms.navigation-panel"
        >
            {children}
        </Frame>
    );
}
