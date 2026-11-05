import type { BotStatus } from "./api";

/** What the screen says about the bot: one line, the notes under it, and the push it offers (null when there is none to offer). */
export interface StatusView {
    headline: string;
    notes: string[];
    push: { label: string; number: number } | null;
    inUse: number[];
    problem: boolean;
}

/** The generated types allow a string for an int32; the server sends a number. */
function whole(value: number | string | null): number | null {
    return value === null ? null : Number(value);
}

export function whenOf(iso: string): string {
    return `${iso.slice(0, 16).replace("T", " ")} UTC`;
}

function lastKnown(status: BotStatus): string {
    const known = whole(status.lastKnown);
    if (status.lastKnownAt === null) return "The bot has not reported a version yet.";
    return `Last known: ${known === null ? "none" : `v${known}`}, at ${whenOf(status.lastKnownAt)}.`;
}

/** The rules of the table in the spec: the number shown is the bot's own answer, never what the CMS last sent. */
export function statusView(status: BotStatus): StatusView {
    const latest = whole(status.latest);
    const has = whole(status.has);
    const answered = status.reach === "Answered";
    const upToDate = answered && latest !== null && has === latest;
    const notes: string[] = [];
    let headline: string;
    let problem = false;

    if (status.reach === "WrongAddress") {
        problem = true;
        headline = "Wrong address";
        notes.push(
            `${status.address} answers as bot '${status.otherBot ?? ""}', not '${status.bot}'. Register the right address for '${status.bot}'; a push will be refused by that bot.`,
        );
    } else if (!answered) {
        problem = true;
        headline = `Bot cannot be reached${latest === null ? "" : ` · Latest v${latest}`}`;
        notes.push(`${status.reason ?? "The bot did not answer."} ${lastKnown(status)}`);
    } else {
        const bot = has === null ? "Bot has none" : `Bot has v${has}`;
        headline = upToDate ? `${bot} · up to date` : latest === null ? bot : `${bot} · Latest v${latest}`;
        if (status.reason !== null) notes.push(`The bot cannot read its store: ${status.reason}`);
        if (status.hasUnknownVersion && has !== null)
            notes.push(`The CMS has no version v${has}: the bot reports a number this CMS never created.`);
    }
    if (latest === null) notes.push("No version yet. Create one: the whole working state is checked and frozen.");

    return {
        headline,
        notes,
        push: latest === null || upToDate ? null : { label: `Push v${latest}`, number: latest },
        inUse: status.inUse.map(Number).filter((number) => number !== has),
        problem,
    };
}
