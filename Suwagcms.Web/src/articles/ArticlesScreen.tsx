import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useSearchParams } from "wouter";

import { useAvailability } from "@/api/availability";
import { ApiError } from "@/api/client";
import { renamer } from "@/api/rename";
import { useResourceHistory } from "@/api/history";
import { usageHref, useUsage } from "@/api/usage";
import { Alert } from "@/design/alert";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { Button } from "@/design/button";
import { ChangeIdDialog } from "@/design/changeIdDialog";
import { mayLeave, useLeaveGuard } from "@/design/leaveGuard";
import { HistoryPanel } from "@/design/historyPanel";
import { UsagePanel } from "@/design/usagePanel";

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
    const [changingId, setChangingId] = useState(false);
    const [location, navigate] = useLocation();
    const [params] = useSearchParams();
    const openParam = params.get("open");
    useLeaveGuard(dirty, DISCARD_QUESTION);
    const usage = useUsage(bot, "articles", open?.kind === "edit" ? open.article.id : null);
    const availability = useAvailability(bot);
    const openId = open?.kind === "edit" ? open.article.id : "";
    const openRevision = open?.kind === "edit" ? revisionOf(open.article) : 0;
    const changer = useMemo(() => renamer(bot, "articles", openId, openRevision), [bot, openId, openRevision]);
    const history = useResourceHistory(
        bot,
        "articles",
        open?.kind === "edit" ? open.article.id : null,
        open?.kind === "edit" ? revisionOf(open.article) : 0,
    );

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

    // Why: a link from another screen, or from the Used by panel, arrives as ?open=<id>; it is taken once and dropped from the address.
    useEffect(() => {
        if (openParam === null) return;
        navigate(location, { replace: true });
        if (dirty && !window.confirm(DISCARD_QUESTION)) return;
        fetchArticle(bot, openParam, new AbortController().signal)
            .then((article) => {
                setOpen({ kind: "edit", article });
            })
            .catch((error: unknown) => {
                setFailure(error instanceof Error ? error.message : String(error));
            });
    }, [openParam, location, navigate, dirty, bot]);

    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (articles === null || bases === null) return <p className="text-muted-foreground">Loading articles…</p>;

    // Why: opening another editor or leaving the screen would throw away what was typed, so it asks first.
    function confirmDiscard(): boolean {
        return mayLeave();
    }

    function remove(article: Article) {
        setDeleteFailure(null);
        deleteArticle(bot, article.id, revisionOf(article))
            .then(() => {
                setOpen(null);
                reload();
            })
            .catch((error: unknown) => {
                setDeleteFailure(
                    error instanceof ApiError
                        ? [error.message, ...error.mistakes.map((m) => `${m.message} ${m.fix}`)].join(" ")
                        : String(error),
                );
            });
    }

    // Why: a chip opens the article it links to in the same editor, after the same question about unsaved changes.
    function openLinked(id: string, kind: "article" | "procedure") {
        if (!confirmDiscard()) return;
        if (kind === "procedure") {
            navigate(`~/bots/${encodeURIComponent(bot)}/procedures/${encodeURIComponent(id)}`);
            return;
        }
        fetchArticle(bot, id, new AbortController().signal)
            .then((article) => {
                setOpen({ kind: "edit", article });
            })
            .catch((error: unknown) => {
                setFailure(error instanceof Error ? error.message : String(error));
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
            isOff={(id) => availability.isOff("article", id)}
            filters={filters}
            onFilters={setFilters}
            onAdd={() => {
                if (confirmDiscard()) setOpen({ kind: "add" });
            }}
            onEdit={(article) => {
                if (confirmDiscard()) setOpen({ kind: "edit", article });
            }}
            onLeave={(event) => {
                if (!confirmDiscard()) event.preventDefault();
            }}
            form={
                <>
                    <AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />
                    {deleteFailure !== null && <Alert variant="destructive">{deleteFailure}</Alert>}
                    {open?.kind === "edit" && (
                        <AvailabilityPanel
                            id={open.article.id}
                            off={availability.isOff("article", open.article.id)}
                            delivery={availability.delivery}
                            usedBy={usage.usage?.usedBy ?? null}
                            onTurn={(off) => {
                                availability.turn("article", open.article.id, off);
                            }}
                        />
                    )}
                    {open?.kind === "edit" && (
                        <div className="flex items-center gap-3">
                            <Button
                                type="button"
                                variant="outline"
                                disabled={dirty}
                                onClick={() => {
                                    setChangingId(true);
                                }}
                            >
                                Change id
                            </Button>
                            {dirty && <span className="text-sm text-muted-foreground">Save your changes first.</span>}
                        </div>
                    )}
                    {changingId && open?.kind === "edit" && (
                        <ChangeIdDialog
                            noun="article"
                            id={open.article.id}
                            preview={changer.preview}
                            apply={changer.apply}
                            onChanged={(id) => {
                                setChangingId(false);
                                setOpen(null);
                                reload();
                                navigate(`${location}?open=${encodeURIComponent(id)}`);
                            }}
                            onCancel={() => {
                                setChangingId(false);
                            }}
                        />
                    )}
                    {open?.kind === "edit" && (
                        <UsagePanel
                            usage={usage.usage}
                            failure={usage.failure}
                            hrefOf={(entry) => usageHref(bot, entry)}
                            onDelete={() => {
                                remove(open.article);
                            }}
                        />
                    )}
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
                            onOpenArticle={openLinked}
                        />
                    )}
                    {open?.kind === "edit" && <HistoryPanel {...history} />}
                </>
            }
        />
    );
}
