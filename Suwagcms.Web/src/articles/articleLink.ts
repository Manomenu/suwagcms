import type { Editor } from "@tiptap/core";
import Mention, { type MentionOptions } from "@tiptap/extension-mention";

/** An article the picker offers: the id is what is stored, the title what the author sees. */
export interface LinkTarget {
    id: string;
    title: string;
    knowledgeBase: string;
}

interface ArticleLinkOptions extends MentionOptions {
    /** The current title of an article, or undefined when this bot has no such article. */
    titleOf: (id: string) => string | undefined;
    onOpen: (id: string) => void;
}

const PREFIX = "cms://article/";
const LINK = /^\[([^\]\n]*)\]\(cms:\/\/article\/([A-Za-z0-9]+)\)/;

/** A title as the label of a link: the brackets and line breaks that would end the label early become spaces. */
export function labelOf(title: string): string {
    return title.replace(/[[\]\r\n]+/g, " ").trim();
}

/**
 * A link to an article is an inline chip in the editor and `[label](cms://article/id)` in the stored Markdown. The label is kept
 * as stored; the chip shows the current title.
 */
export const ArticleLink = Mention.extend<ArticleLinkOptions>({
    name: "articleLink",

    addOptions() {
        return {
            // Why: the parent options carry the HTML attributes and the suggestion every mention has.
            ...(this.parent?.() as MentionOptions),
            titleOf: () => undefined,
            onOpen: () => undefined,
        };
    },

    markdownTokenName: "articleLink",

    markdownTokenizer: {
        name: "articleLink",
        level: "inline",
        start: (source: string) => source.indexOf("["),
        tokenize: (source: string) => {
            const match = LINK.exec(source);
            return match === null ? undefined : { type: "articleLink", raw: match[0], label: match[1] ?? "", id: match[2] ?? "" };
        },
    },

    parseMarkdown: (token, helpers) => helpers.createNode("articleLink", { id: String(token["id"]), label: String(token["label"]) }),

    renderMarkdown: (node) => {
        const attrs = (node.attrs ?? {}) as { id?: string; label?: string };
        return `[${attrs.label ?? ""}](${PREFIX}${attrs.id ?? ""})`;
    },

    addNodeView() {
        const { titleOf, onOpen } = this.options;
        return ({ node }) => {
            const id = String(node.attrs["id"]);
            const dom = document.createElement("span");
            dom.contentEditable = "false";
            dom.setAttribute("role", "link");
            dom.dataset["articleLink"] = id;
            dom.className = "mx-0.5 inline-block cursor-pointer rounded bg-primary/10 px-1.5 text-primary hover:bg-primary/20";
            dom.textContent = titleOf(id) ?? String(node.attrs["label"] ?? id);
            dom.addEventListener("click", () => {
                onOpen(id);
            });
            return { dom };
        };
    },
});

/** Puts the picker's trigger at the cursor, with the space before it that the picker needs to open. */
export function openPicker(editor: Editor): void {
    const { $from } = editor.state.selection;
    const before = $from.parent.textBetween(Math.max(0, $from.parentOffset - 1), $from.parentOffset);
    editor
        .chain()
        .focus()
        .insertContent(before === "" || before === " " ? "@" : " @")
        .run();
}
