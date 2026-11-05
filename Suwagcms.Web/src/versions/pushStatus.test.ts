import { describe, expect, it } from "vitest";

import type { BotStatus } from "./api";
import { otherVersions, statusView } from "./pushStatus";

function status(over: Partial<BotStatus>): BotStatus {
    return {
        bot: "rzeczoznawca",
        address: "http://rzeczoznawca.internal:8080",
        reach: "Answered",
        has: 12,
        held: [12],
        inUse: [],
        lastKnown: 12,
        lastKnownAt: "2026-11-04T21:40:00+00:00",
        reason: null,
        otherBot: null,
        latest: 14,
        hasUnknownVersion: false,
        attempts: [],
        ...over,
    };
}

describe("statusView", () => {
    it("AC-version-push-3: offers the latest version when the bot has an older one", () => {
        const view = statusView(status({}));

        expect(view.headline).toBe("Bot has v12 · Latest v14");
        expect(view.push).toEqual({ label: "Push v14", number: 14 });
    });

    it("AC-version-push-3: offers the latest version when the bot has none", () => {
        const view = statusView(status({ has: null, held: [] }));

        expect(view.headline).toBe("Bot has none · Latest v14");
        expect(view.push?.label).toBe("Push v14");
    });

    it("AC-version-push-3: offers nothing when the bot is up to date", () => {
        const view = statusView(status({ has: 14 }));

        expect(view.headline).toBe("Bot has v14 · up to date");
        expect(view.push).toBeNull();
    });

    it("AC-version-push-3: offers nothing, and points to creating one, when the CMS has no version", () => {
        const view = statusView(status({ latest: null }));

        expect(view.push).toBeNull();
        expect(view.notes.join(" ")).toContain("Create one");
    });

    it("AC-version-push-4: says the bot cannot be reached, with the last number it reported and when, and still offers the push", () => {
        const view = statusView(status({ reach: "Unreachable", has: null, reason: "cannot reach http://rzeczoznawca.internal:8080" }));

        expect(view.headline).toContain("cannot be reached");
        expect(view.notes.join(" ")).toContain("Last known: v12, at 2026-11-04 21:40 UTC.");
        expect(view.push?.label).toBe("Push v14");
    });

    it("AC-version-push-8: names the other bot a wrong address answers as", () => {
        const view = statusView(status({ reach: "WrongAddress", has: null, otherBot: "sklep" }));

        expect(view.headline).toBe("Wrong address");
        expect(view.notes.join(" ")).toContain("answers as bot 'sklep', not 'rzeczoznawca'");
    });

    it("AC-version-push-2: shows a number the CMS does not know as the bot reports it, with a note, and offers the push", () => {
        const view = statusView(status({ has: 99, hasUnknownVersion: true }));

        expect(view.headline).toBe("Bot has v99 · Latest v14");
        expect(view.notes.join(" ")).toContain("The CMS has no version v99");
        expect(view.push?.number).toBe(14);
    });

    it("AC-version-push-13: lists the versions in use by conversations other than the current one", () => {
        const view = statusView(status({ has: 14, held: [11, 14], inUse: [11, 14] }));

        expect(view.inUse).toEqual([11]);
    });
});

describe("otherVersions", () => {
    it("AC-version-rollback-13: lists the kept versions other than the latest and the one the bot has, newest first", () => {
        expect(otherVersions([11, 14, 12, 13], status({ has: 12, latest: 14 }))).toEqual([13, 11]);
    });

    it("AC-version-rollback-13: offers what the bot left behind after a rollback, also a version newer than the bot's", () => {
        expect(otherVersions([14, 13, 12], status({ has: 12, latest: 14 }))).toEqual([13]);
        expect(otherVersions([14, 13, 12], status({ has: null, latest: 14 }))).toEqual([13, 12]);
    });
});
