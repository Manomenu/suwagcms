import { useCallback, useEffect, useState } from "react";
import { useLocation } from "wouter";

import { useAvailability } from "@/api/availability";
import { useResourceEditor } from "@/api/resourceEditor";
import { Alert } from "@/design/alert";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { EditorPanels } from "@/design/editorPanels";

import { articlesApi, fetchArticles, fetchKnowledgeBases, type Filters, type KnowledgeBase } from "./api";
import { emptyForm, formOf } from "./articleForm";
import { ArticleForm } from "./ArticleForm";
import { ArticlesView } from "./ArticlesView";

export function ArticlesScreen({ bot }: { bot: string }) {
    const [bases, setBases] = useState<KnowledgeBase[] | null>(null);
    const [basesFailure, setBasesFailure] = useState<string | null>(null);
    const [filters, setFilters] = useState<Filters>({ knowledgeBase: "", language: "", text: "" });
    const [, navigate] = useLocation();
    const availability = useAvailability(bot);
    const list = useCallback((signal: AbortSignal) => fetchArticles(bot, filters, signal), [bot, filters]);
    const editor = useResourceEditor({
        bot,
        api: articlesApi,
        idOf: (article) => article.id,
        usage: "articles",
        list,
        revisionOfOpened: true,
        onRenamed: availability.reload,
    });

    useEffect(() => {
        const controller = new AbortController();
        fetchKnowledgeBases(bot, controller.signal)
            .then(setBases)
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setBasesFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [bot]);

    const failure = editor.failure ?? basesFailure;
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (editor.items === null || bases === null) return <p className="text-muted-foreground">Loading articles…</p>;

    // Why: a chip opens the article it links to in the same editor, after the same question about unsaved changes.
    function openLinked(id: string, kind: "article" | "procedure") {
        if (!editor.mayLeave()) return;
        if (kind === "procedure") navigate(`~/bots/${encodeURIComponent(bot)}/procedures/${encodeURIComponent(id)}`);
        else editor.openById(id);
    }

    const article = editor.item;
    return (
        <ArticlesView
            bot={bot}
            articles={editor.items}
            bases={bases}
            isOff={(id) => availability.isOff("article", id)}
            filters={filters}
            onFilters={setFilters}
            onAdd={editor.add}
            onEdit={editor.edit}
            onLeave={(event) => {
                if (!editor.mayLeave()) event.preventDefault();
            }}
            form={
                <EditorPanels
                    noun="article"
                    {...editor.panels}
                    notice={
                        <AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />
                    }
                    panel={
                        article !== null && (
                            <AvailabilityPanel
                                id={article.id}
                                off={availability.isOff("article", article.id)}
                                delivery={availability.delivery}
                                usedBy={editor.panels.usage?.usage?.usedBy ?? null}
                                onTurn={(off) => {
                                    availability.turn("article", article.id, off);
                                }}
                            />
                        )
                    }
                >
                    {editor.isOpen && (
                        <ArticleForm
                            key={article?.id ?? "add"}
                            bot={bot}
                            bases={bases}
                            initial={
                                article === null
                                    ? emptyForm(filters.knowledgeBase !== "" ? filters.knowledgeBase : (bases[0]?.id ?? ""))
                                    : formOf(article)
                            }
                            {...editor.formProps}
                            onReload={() => {
                                if (article !== null) {
                                    editor.reload();
                                    editor.openById(article.id);
                                }
                            }}
                            onOpenArticle={openLinked}
                        />
                    )}
                </EditorPanels>
            }
        />
    );
}
