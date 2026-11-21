import { Alert } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";
import { useMemo } from "react";
import { Redirect, useLocation, useSearchParams } from "wouter";

import { useAvailability } from "@/api/availability";
import { messageOf } from "@/api/client";
import { editHref } from "@/api/resourceAddress";
import { useResourceEditor } from "@/api/resourceEditor";
import { AvailabilityNotice, AvailabilityPanel } from "@/design/availabilityPanel";
import { ResourceEdit } from "@/design/resourceEdit";

import { articlesApi, fetchArticles, fetchKnowledgeBases, type Filters, type KnowledgeBase } from "./api";
import { emptyForm, formOf } from "./articleForm";
import { ArticleForm } from "./ArticleForm";
import { ArticlesView } from "./ArticlesView";

export function ArticlesScreen({ bot }: { bot: string }) {
    const basesQuery = useQuery({
        queryKey: ["resources", bot, "knowledge-bases", "list", null],
        queryFn: ({ signal }) => fetchKnowledgeBases(bot, signal),
    });
    const bases: KnowledgeBase[] | null = basesQuery.data ?? null;
    const basesFailure = basesQuery.error === null ? null : messageOf(basesQuery.error);
    const [params, setParams] = useSearchParams();
    const knowledgeBase = params.get("knowledgeBase") ?? "";
    const language = params.get("language") ?? "";
    const text = params.get("text") ?? "";
    const filters: Filters = { knowledgeBase, language, text };
    const [, navigate] = useLocation();
    const availability = useAvailability(bot);
    const list = useMemo(
        () => ({
            key: [knowledgeBase, language, text],
            fetch: (signal: AbortSignal) => fetchArticles(bot, { knowledgeBase, language, text }, signal),
        }),
        [bot, knowledgeBase, language, text],
    );
    const editor = useResourceEditor({
        bot,
        api: articlesApi,
        idOf: (article) => article.id,
        usage: "articles",
        list,
        revisionOfOpened: true,
        onRenamed: availability.reload,
    });

    if (editor.redirect !== null) return <Redirect to={editor.redirect} replace />;
    const failure = editor.failure ?? basesFailure;
    if (failure !== null) return <Alert variant="destructive">{failure}</Alert>;
    if (editor.items === null || bases === null) return <p className="text-muted-foreground">Loading articles…</p>;

    function openLinked(id: string, kind: "article" | "procedure") {
        if (!editor.mayLeave()) return;
        if (kind === "procedure") navigate(`~${editHref(bot, "procedures", id)}`);
        else editor.openById(id);
    }

    const notice = <AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />;
    if (!editor.isOpen) {
        return (
            <ArticlesView
                bot={bot}
                history={editor.listHistory}
                articles={editor.items}
                bases={bases}
                isOff={(id) => availability.isOff("article", id)}
                filters={filters}
                notice={notice}
                onFilters={(next) => {
                    setParams(Object.fromEntries(Object.entries(next).filter(([, value]) => value !== "")), { replace: true });
                }}
                addHref={editor.addHref}
                hrefOf={editor.hrefOf}
                onLeave={(event) => {
                    if (!editor.mayLeave()) event.preventDefault();
                }}
            />
        );
    }

    const article = editor.item;
    return (
        <ResourceEdit
            noun="article"
            plural="articles"
            addTitle="Add an article"
            {...editor.panels}
            notice={notice}
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
            <ArticleForm
                key={article?.id ?? "add"}
                bot={bot}
                bases={bases}
                initial={article === null ? emptyForm(knowledgeBase !== "" ? knowledgeBase : (bases[0]?.id ?? "")) : formOf(article)}
                {...editor.formProps}
                onReload={() => {
                    if (article !== null) {
                        editor.reload();
                        editor.openById(article.id);
                    }
                }}
                onOpenArticle={openLinked}
            />
        </ResourceEdit>
    );
}
