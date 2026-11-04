import { useCallback, useEffect, useState } from "react";

import { ApiError } from "@/api/client";
import { Alert } from "@/design/alert";

import {
    deleteArticle,
    fetchArticle,
    fetchArticles,
    fetchKnowledgeBases,
    revisionOf,
    type Article,
    type Filters,
    type KnowledgeBase,
} from "./api";
import { emptyForm, formOf } from "./articleForm";
import { ArticleForm, DISCARD_QUESTION } from "./ArticleForm";
import { ArticlesView } from "./ArticlesView";

type Open = { kind: "add" } | { kind: "edit"; article: Article } | null;

/** Loads the articles of the bot in the address when it opens, so articles added meanwhile from the CLI or another tab are found. */
export function ArticlesScreen({ bot }: { bot: string }) {
    const [articles, setArticles] = useState<Article[] | null>(null);
    const [bases, setBases] = useState<KnowledgeBase[] | null>(null);
    const [filters, setFilters] = useState<Filters>({ knowledgeBase: "", language: "", text: "" });
    const [failure, setFailure] = useState<string | null>(null);
    const [open, setOpen] = useState<Open>(null);
    const [dirty, setDirty] = useState(false);
    const [deleteFailure, setDeleteFailure] = useState<string | null>(null);

    const fail = useCallback((signal: AbortSignal) => {
        return (error: unknown) => {
            if (!signal.aborted) setFailure(error instanceof Error ? error.message : String(error));
        };
    }, []);

    const loadBases = useCallback(
        (signal: AbortSignal) => {
            fetchKnowledgeBases(bot, signal).then(setBases).catch(fail(signal));
        },
        [bot, fail],
    );

    const loadArticles = useCallback(
        (signal: AbortSignal) => {
            fetchArticles(bot, filters, signal).then(setArticles).catch(fail(signal));
        },
        [bot, filters, fail],
    );

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        loadBases(controller.signal);
        return () => {
            controller.abort();
        };
    }, [loadBases]);

    useEffect(() => {
        const controller = new AbortController();
        loadArticles(controller.signal);
        return () => {
            controller.abort();
        };
    }, [loadArticles]);

    const reload = useCallback(() => {
        loadArticles(new AbortController().signal);
    }, [loadArticles]);

    if (failure !== null) {
        return (
            <main className="mx-auto max-w-2xl p-4">
                <Alert variant="destructive">{failure}</Alert>
            </main>
        );
    }
    if (articles === null || bases === null) return <main className="mx-auto max-w-2xl p-4 text-muted-foreground">Loading articles…</main>;

    // Why: opening another editor or leaving the screen would throw away what was typed, so it asks first.
    function confirmDiscard(): boolean {
        return !dirty || window.confirm(DISCARD_QUESTION);
    }

    function remove(article: Article) {
        setDeleteFailure(null);
        deleteArticle(bot, article.id, revisionOf(article))
            .then(reload)
            .catch((error: unknown) => {
                setDeleteFailure(
                    error instanceof ApiError
                        ? [error.message, ...error.mistakes.map((m) => `${m.message} ${m.fix}`)].join(" ")
                        : String(error),
                );
            });
    }

    function reloadEdited(id: string) {
        reload();
        fetchArticle(bot, id, new AbortController().signal)
            .then((article) => {
                setOpen({ kind: "edit", article });
            })
            .catch((error: unknown) => {
                setFailure(error instanceof Error ? error.message : String(error));
            });
    }

    return (
        <ArticlesView
            bot={bot}
            articles={articles}
            bases={bases}
            filters={filters}
            onFilters={setFilters}
            onAdd={() => {
                if (confirmDiscard()) setOpen({ kind: "add" });
            }}
            onEdit={(article) => {
                if (confirmDiscard()) setOpen({ kind: "edit", article });
            }}
            onDelete={remove}
            onLeave={(event) => {
                if (!confirmDiscard()) event.preventDefault();
            }}
            form={
                <>
                    {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
                    {open !== null && (
                        <ArticleForm
                            key={open.kind === "edit" ? open.article.id : "add"}
                            bot={bot}
                            bases={bases}
                            initial={
                                open.kind === "edit"
                                    ? formOf(open.article)
                                    : emptyForm(filters.knowledgeBase !== "" ? filters.knowledgeBase : (bases[0]?.id ?? ""))
                            }
                            editing={open.kind === "edit"}
                            revision={open.kind === "edit" ? revisionOf(open.article) : 0}
                            onSaved={() => {
                                setOpen(null);
                                reload();
                            }}
                            onReload={() => {
                                if (open.kind === "edit") reloadEdited(open.article.id);
                            }}
                            onCancel={() => {
                                setOpen(null);
                            }}
                            onDirtyChange={setDirty}
                        />
                    )}
                </>
            }
        />
    );
}
