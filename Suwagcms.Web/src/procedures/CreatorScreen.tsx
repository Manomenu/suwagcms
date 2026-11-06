import { useEffect, useState } from "react";
import { useSearch } from "wouter";

import { ApiError } from "@/api/client";
import { fetchDeclaration } from "@/api/declaration";
import { listHref, withListSearch } from "@/api/resourceAddress";
import { Alert } from "@/design/alert";
import { Anchor } from "@/design/anchor";

import {
    fetchActions,
    fetchArticles,
    fetchFacts,
    fetchLayout,
    fetchProcedure,
    fetchProcedures,
    type ActionView,
    type Fact,
    type ProcedureDetail,
} from "./api";
import { Creator } from "./Creator";
import type { LinkableArticle } from "./draft";
import type { Positions } from "./layout";
import { offeredBlocks, type OfferedBlocks } from "./offeredBlocks";

interface Loaded {
    detail: ProcedureDetail;
    facts: Fact[];
    procedures: string[];
    articles: LinkableArticle[];
    actions: ActionView[];
    stored: Positions;
    offered: OfferedBlocks;
}

/** Loads the procedure, the bot's facts and the stored positions when the creator opens, so what an agent saved meanwhile is found. */
export function CreatorScreen({ bot, id }: { bot: string; id: string }) {
    const search = useSearch();
    const [loaded, setLoaded] = useState<Loaded | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [missing, setMissing] = useState(false);

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        Promise.all([
            fetchProcedure(bot, id, controller.signal),
            fetchFacts(bot, controller.signal),
            fetchLayout(bot, id, controller.signal),
            fetchProcedures(bot, controller.signal),
            fetchArticles(bot, controller.signal),
            // Why: the last answer, not a new ask: opening a form must not wait on a bot.
            fetchDeclaration(bot, controller.signal, false),
            fetchActions(bot, controller.signal),
        ])
            .then(([detail, facts, layout, list, articles, declaration, actions]) => {
                setLoaded({
                    detail,
                    facts,
                    procedures: list.procedures.map((procedure) => procedure.id),
                    articles: articles.map((article) => ({ id: article.id, title: article.title })),
                    actions,
                    offered: offeredBlocks(declaration),
                    stored: Object.fromEntries(
                        Object.entries(layout.positions).map(([name, at]) => [name, { x: Number(at.x), y: Number(at.y) }]),
                    ),
                });
            })
            .catch((error: unknown) => {
                if (controller.signal.aborted) return;
                if (error instanceof ApiError && error.status === 404) setMissing(true);
                else setFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [bot, id]);

    if (missing) {
        return (
            <div className="flex flex-col gap-4 p-4">
                <Anchor href={`~${withListSearch(listHref(bot, "procedures"), search)}`}>← Back to procedures</Anchor>
                <Alert variant="destructive">This bot has no procedure '{id}'. It may have been deleted, or its id changed.</Alert>
            </div>
        );
    }
    if (failure !== null) {
        return (
            <div className="p-4">
                <Alert variant="destructive">{failure}</Alert>
            </div>
        );
    }
    if (loaded === null) return <p className="p-4 text-muted-foreground">Loading {id}…</p>;
    return (
        <Creator
            bot={bot}
            id={id}
            initial={loaded.detail}
            facts={loaded.facts}
            procedures={loaded.procedures}
            articles={loaded.articles}
            actions={loaded.actions}
            stored={loaded.stored}
            offered={loaded.offered}
        />
    );
}
