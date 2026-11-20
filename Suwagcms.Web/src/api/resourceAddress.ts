export type AddressedCollection = "facts" | "knowledge-bases" | "articles" | "procedures" | "actions";

export type ResourceAddress = { screen: "list" } | { screen: "new" } | { screen: "edit"; id: string };

export function listHref(bot: string, collection: AddressedCollection): string {
    return `/bots/${encodeURIComponent(bot)}/${collection}`;
}

export function newHref(bot: string, collection: AddressedCollection): string {
    return `${listHref(bot, collection)}/new`;
}

export function editHref(bot: string, collection: AddressedCollection, id: string): string {
    return `${listHref(bot, collection)}/${encodeURIComponent(id)}/edit`;
}

export const HISTORY_PAGE = "historyPage";

export function withListSearch(href: string, search: string): string {
    const kept = new URLSearchParams(search);
    kept.delete(HISTORY_PAGE);
    kept.delete("step");
    kept.delete("if");
    kept.delete("compare");
    const text = kept.toString();
    return text === "" ? href : `${href}?${text}`;
}

export function versionsHref(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/versions`;
}

export function versionHref(bot: string, number: number): string {
    return `${versionsHref(bot)}/${number}`;
}

function decoded(part: string): string {
    try {
        return decodeURIComponent(part);
    } catch {
        return part;
    }
}

export function addressOf(pathname: string, collection: AddressedCollection): ResourceAddress {
    const parts = pathname.split("/").filter((part) => part !== "");
    const [, , own, first, second] = parts;
    if (own !== collection || first === undefined) return { screen: "list" };
    if (first === "new" && second === undefined) return { screen: "new" };
    if (second === "edit" && parts.length === 5) return { screen: "edit", id: decoded(first) };
    return { screen: "list" };
}
