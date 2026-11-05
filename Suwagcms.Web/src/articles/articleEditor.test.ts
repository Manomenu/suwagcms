// @vitest-environment jsdom
import { Editor } from "@tiptap/core";
import { describe, expect, it } from "vitest";

import { extensionsOf } from "./articleEditor";
import type { LinkTarget } from "./articleLink";

const targets: LinkTarget[] = [
    { id: "cennik", title: "Prices", knowledgeBase: "offer" },
    { id: "wycena", title: "Valuation", knowledgeBase: "faq" },
];

function editorOf(markdown: string, opened: string[] = []): Editor {
    return new Editor({
        extensions: extensionsOf({ targets: () => targets, selfId: () => "wycena", onOpen: (id) => opened.push(id) }),
        content: markdown,
        contentType: "markdown",
    });
}

function roundTrip(markdown: string): string {
    const editor = editorOf(markdown);
    const out = editor.getMarkdown();
    editor.destroy();
    return out;
}

const SET = {
    headings: "# One\n\n## Two\n\n### Three\n\nText under it.",
    bullets: "- first\n- second\n  - nested\n- third",
    numbered: "1. one\n2. two\n3. three",
    emphasis: "Some **bold**, some *slanted* and some `code`.",
    table: "| Kind | Price |\n| --- | --- |\n| Flat | 100 |\n| House | 200 |",
    links: "See the [Price list](cms://article/cennik) and [the web](https://example.com/a?b=1).",
    several: "A [one](cms://article/cennik), then [two](cms://article/wycena).\n\nNext paragraph with [one](cms://article/cennik) again.",
    inBullet: "- ask about [Prices](cms://article/cennik)\n- then stop",
    inCode: "```\n[x](cms://article/cennik)\n```\n\nA span `[x](cms://article/cennik)` stays text.",
    tableInText: "Before.\n\n| Kind | Price |\n| --- | --- |\n| Flat | 100 |\n\nAfter.",
    tableAligned: "| Kind | Price |\n| :--- | ---: |\n| Flat | 100 |",
    quote: "> Quoted\n\nAfter.",
    polish: "# Cennik\n\nWycena mieszkania: zażółć gęślą jaźń.",
};

describe("the Markdown the editor reads and writes", () => {
    for (const [name, markdown] of Object.entries(SET)) {
        it(`AC-article-links-3: ${name} come back from the editor as they went in`, () => {
            expect(roundTrip(markdown)).toBe(markdown);
        });
    }

    it("AC-article-links-3: opening a text the editor would write differently does not report a change, so it is saved as stored", () => {
        const changes: string[] = [];
        const editor = new Editor({
            extensions: extensionsOf({ targets: () => targets, selfId: () => "wycena", onOpen: () => undefined }),
            content: "* star\n* list\n\nSome _emphasis_.",
            contentType: "markdown",
            onUpdate: ({ editor: changed }) => changes.push(changed.getMarkdown()),
        });

        expect(changes).toEqual([]);
        expect(editor.getMarkdown()).toBe("- star\n- list\n\nSome *emphasis*.");
        editor.destroy();
    });

    it("AC-article-links-3: a text with CRLF comes back with the same words and structure, its breaks as the editor writes them", () => {
        expect(roundTrip("# Cennik\r\n\r\n- one\r\n- [Prices](cms://article/cennik)")).toBe(
            "# Cennik\n\n- one\n- [Prices](cms://article/cennik)",
        );
    });

    it("AC-article-links-6: a link written as Markdown is a chip with the id, and the chip shows the current title", () => {
        const editor = editorOf("See [Old name](cms://article/cennik).");
        const chip = editor.view.dom.querySelector("[data-article-link]");

        expect(chip?.getAttribute("data-article-link")).toBe("cennik");
        expect(chip?.textContent).toBe("Prices");
        expect(editor.getMarkdown()).toBe("See [Old name](cms://article/cennik).");
        editor.destroy();
    });

    it("AC-article-links-6: a cms address in code is not a chip", () => {
        const editor = editorOf(SET.inCode);

        expect(editor.view.dom.querySelectorAll("[data-article-link]")).toHaveLength(0);
        editor.destroy();
    });

    it("AC-article-links-5: clicking a chip opens its article and removing the chip removes the link from the text", () => {
        const opened: string[] = [];
        const editor = editorOf("See [Prices](cms://article/cennik).", opened);
        editor.view.dom.querySelector<HTMLElement>("[data-article-link]")?.click();
        expect(opened).toEqual(["cennik"]);

        editor.commands.command(({ tr, state }) => {
            state.doc.descendants((node, position) => {
                if (node.type.name === "articleLink") tr.delete(position, position + node.nodeSize);
            });
            return true;
        });

        expect(editor.getMarkdown()).toBe("See .");
        editor.destroy();
    });
});
