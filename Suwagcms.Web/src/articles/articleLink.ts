import type { Editor } from "@tiptap/core";
import Mention, { type MentionOptions } from "@tiptap/extension-mention";

// Why: a procedure has no title, so its id stands in for what the author sees.
export interface LinkTarget {
    id: string;
    title: string;
    knowledgeBase: string;
    kind?: "procedure";
    description?: string;
}

type LinkKind = "article" | "procedure";

interface ArticleLinkOptions extends MentionOptions {
    /** The current title of an article, or undefined when this bot has no such article. */
    titleOf: (id: string) => string | undefined;
    onOpen: (id: string, kind: LinkKind) => void;
}

const LINK = /^\[([^\]\n]*)\]\(cms:\/\/(article|procedure)\/([A-Za-z0-9]+)\)/;

/** A title as the label of a link: the brackets and line breaks that would end the label early become spaces. */
export function labelOf(title: string): string {
    return title.replace(/[[\]\r\n]+/g, " ").trim();
}

// Why: the label is kept as stored, so opening and saving changes nothing; the chip shows an article's current title, or a procedure's id, which is all a procedure has.
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

    addAttributes() {
        return {
            ...this.parent?.(),
            kind: { default: "article", rendered: false },
        };
    },

    markdownTokenName: "articleLink",

    markdownTokenizer: {
        name: "articleLink",
        level: "inline",
        start: (source: string) => source.indexOf("["),
        tokenize: (source: string) => {
            const match = LINK.exec(source);
            return match === null
                ? undefined
                : { type: "articleLink", raw: match[0], label: match[1] ?? "", kind: match[2] ?? "", id: match[3] ?? "" };
        },
    },

    parseMarkdown: (token, helpers) =>
        helpers.createNode("articleLink", { id: String(token["id"]), label: String(token["label"]), kind: String(token["kind"]) }),

    renderMarkdown: (node) => {
        const attrs = (node.attrs ?? {}) as { id?: string; label?: string; kind?: LinkKind };
        return `[${attrs.label ?? ""}](cms://${attrs.kind ?? "article"}/${attrs.id ?? ""})`;
    },

    addNodeView() {
        const { titleOf, onOpen } = this.options;
        return ({ node }) => {
            const id = String(node.attrs["id"]);
            const kind = node.attrs["kind"] === "procedure" ? "procedure" : "article";
            const dom = document.createElement("span");
            dom.contentEditable = "false";
            dom.setAttribute("role", "link");
            dom.dataset[kind === "procedure" ? "procedureLink" : "articleLink"] = id;
            dom.className =
                "mx-0.5 inline-block cursor-pointer rounded-full bg-primary-light px-2 text-primary-light-foreground hover:bg-primary-light-hover";
            dom.textContent = kind === "procedure" ? id : (titleOf(id) ?? String(node.attrs["label"] ?? id));
            dom.addEventListener("click", () => {
                onOpen(id, kind);
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
