export const TABS = [
    { id: "facts", label: "Facts" },
    { id: "knowledge-bases", label: "Knowledge bases" },
    { id: "articles", label: "Articles" },
    { id: "procedures", label: "Procedures" },
    { id: "actions", label: "Actions" },
    { id: "persona", label: "Persona" },
    { id: "versions", label: "Versions" },
    { id: "history", label: "History" },
] as const;

export type Tab = (typeof TABS)[number]["id"];

export interface Navigation {
    bot: string | null;
    tab: Tab | null;
    fills: boolean;
}

function decode(part: string): string {
    // Why: a malformed % in the address must show the page, not crash the frame.
    try {
        return decodeURIComponent(part);
    } catch {
        return part;
    }
}

function isTab(part: string | undefined): part is Tab {
    return TABS.some((tab) => tab.id === part);
}

export function navigationOf(pathname: string): Navigation {
    const parts = pathname.split("/").filter((part) => part !== "");
    if (parts[0] !== "bots" || parts[1] === undefined) return { bot: null, tab: null, fills: false };
    const bot = decode(parts[1]);
    const tab = isTab(parts[2]) ? parts[2] : null;
    return { bot, tab, fills: tab === "procedures" && parts[3] !== undefined };
}

export function botHref(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}`;
}

export function tabHref(bot: string, tab: Tab): string {
    return `${botHref(bot)}/${tab}`;
}
