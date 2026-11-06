// Why: every screen, link and redirect must agree on where a resource lives, so its addresses are built and read in this one place.
export type AddressedCollection = "facts" | "knowledge-bases" | "articles" | "procedures" | "actions";

/** What the address asks of a resource's screen. */
export type ResourceAddress = { screen: "list" } | { screen: "new" } | { screen: "edit"; id: string };

export function listHref(bot: string, collection: AddressedCollection): string {
    return `/bots/${encodeURIComponent(bot)}/${collection}`;
}

export function newHref(bot: string, collection: AddressedCollection): string {
    return `${listHref(bot, collection)}/new`;
}

/** The id comes before `edit`, so the last part of the address never holds a dot that reads as a file name, as `customer.email` would. */
export function editHref(bot: string, collection: AddressedCollection, id: string): string {
    return `${listHref(bot, collection)}/${encodeURIComponent(id)}/edit`;
}

function decoded(part: string): string {
    // Why: a malformed % in the address must show the "not found" of that id, not crash the page.
    try {
        return decodeURIComponent(part);
    } catch {
        return part;
    }
}

/** Reads a whole path; anything that is not `new` or `<id>/edit` under the collection is its list. */
export function addressOf(pathname: string, collection: AddressedCollection): ResourceAddress {
    const parts = pathname.split("/").filter((part) => part !== "");
    const [, , own, first, second] = parts;
    if (own !== collection || first === undefined) return { screen: "list" };
    if (first === "new" && second === undefined) return { screen: "new" };
    if (second === "edit" && parts.length === 5) return { screen: "edit", id: decoded(first) };
    return { screen: "list" };
}
