import type { SuggestionOptions, SuggestionProps } from "@tiptap/suggestion";

import { labelOf, type LinkTarget } from "./articleLink";

const LIMIT = 20;

// Why: conversation cannot be started, so it is never offered.
export function linkTargets(
    articles: { id: string; title: string; knowledgeBase: string }[],
    procedures: { id: string; description: string }[],
): LinkTarget[] {
    return [
        ...articles.map((article) => ({ id: article.id, title: article.title, knowledgeBase: article.knowledgeBase })),
        ...procedures
            .filter((procedure) => procedure.id !== "conversation")
            .map((procedure) => ({
                id: procedure.id,
                title: procedure.id,
                knowledgeBase: "",
                kind: "procedure" as const,
                description: procedure.description,
            })),
    ];
}

// Why: only the guide may hold a link to a procedure, because a customer reads content.
export function targetsFor(field: "content" | "llmGuide", targets: LinkTarget[]): LinkTarget[] {
    return field === "llmGuide" ? targets : targets.filter((target) => target.kind !== "procedure");
}

// Why: a procedure may share its id with the article being edited, and it is not that article.
export function matching(targets: LinkTarget[], query: string, selfId: string): LinkTarget[] {
    const needle = query.trim().toLowerCase();
    return targets
        .filter((target) => target.kind === "procedure" || target.id !== selfId)
        .filter(
            (target) =>
                needle === "" ||
                target.title.toLowerCase().includes(needle) ||
                target.id.toLowerCase().includes(needle) ||
                (target.description ?? "").toLowerCase().includes(needle),
        )
        .slice(0, LIMIT);
}

interface Item {
    id: string;
    label: string;
    title: string;
    knowledgeBase: string;
    kind: "article" | "procedure";
    description: string;
}

/** The suggestion of the link: `@` after a space or at a line start opens a list of this bot's articles under the cursor. */
export function pickerOf(targets: () => LinkTarget[], selfId: () => string): Omit<SuggestionOptions<Item>, "editor"> {
    return {
        char: "@",
        items: ({ query }) =>
            matching(targets(), query, selfId()).map((target) => ({
                id: target.id,
                label: labelOf(target.title),
                title: target.title,
                knowledgeBase: target.knowledgeBase,
                kind: target.kind ?? "article",
                description: target.description ?? "",
            })),
        render: () => {
            let list: HTMLUListElement | null = null;
            let chosen = 0;
            let last: SuggestionProps<Item> | null = null;

            function paint() {
                if (list === null || last === null) return;
                const props = last;
                list.replaceChildren(
                    ...(props.items.length === 0
                        ? [element("li", "px-2 py-1 text-muted-foreground", "Nothing matches")]
                        : props.items.map((item, index) => {
                              const row = element("li", "cursor-pointer rounded px-2 py-1 hover:bg-accent", `${item.title}  `);
                              row.setAttribute("role", "option");
                              row.dataset["articleOption"] = item.id;
                              row.setAttribute("aria-selected", String(index === chosen));
                              row.classList.toggle("bg-accent", index === chosen);
                              row.prepend(element("span", "mr-1 text-xs font-medium text-muted-foreground", item.kind));
                              row.append(
                                  element(
                                      "span",
                                      "text-xs text-muted-foreground",
                                      item.kind === "procedure" ? item.description : `${item.id} · ${item.knowledgeBase}`,
                                  ),
                              );
                              row.addEventListener("mousedown", (event) => {
                                  event.preventDefault();
                                  props.command(item);
                              });
                              return row;
                          })),
                );
                const rect = props.clientRect?.();
                if (rect) {
                    list.style.left = `${rect.left}px`;
                    list.style.top = `${rect.bottom + 4}px`;
                }
            }

            return {
                onStart: (props) => {
                    list = document.createElement("ul");
                    list.setAttribute("role", "listbox");
                    list.setAttribute("aria-label", "What to link");
                    list.className =
                        "fixed z-50 max-h-64 min-w-64 overflow-auto rounded-md border border-border bg-secondary p-1 text-sm text-foreground shadow-md";
                    document.body.append(list);
                    chosen = 0;
                    last = props;
                    paint();
                },
                onUpdate: (props) => {
                    chosen = 0;
                    last = props;
                    paint();
                },
                onKeyDown: ({ event }) => {
                    const items = last?.items ?? [];
                    if (event.key === "Escape") {
                        list?.remove();
                        list = null;
                        return true;
                    }
                    if (items.length === 0) return false;
                    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
                        chosen = (chosen + (event.key === "ArrowDown" ? 1 : items.length - 1)) % items.length;
                        paint();
                        return true;
                    }
                    if (event.key === "Enter" || event.key === "Tab") {
                        const item = items[chosen];
                        if (item) last?.command(item);
                        return true;
                    }
                    return false;
                },
                onExit: () => {
                    list?.remove();
                    list = null;
                },
            };
        },
    };
}

function element(tag: string, className: string, text: string): HTMLElement {
    const created = document.createElement(tag);
    created.className = className;
    created.textContent = text;
    return created;
}
