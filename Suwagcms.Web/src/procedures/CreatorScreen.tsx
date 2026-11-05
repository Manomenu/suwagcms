import { useEffect, useState } from "react";

import { Alert } from "@/design/alert";

import { fetchArticles, fetchFacts, fetchLayout, fetchProcedure, fetchProcedures, type Fact, type ProcedureDetail } from "./api";
import { Creator } from "./Creator";
import type { LinkableArticle } from "./draft";
import type { Positions } from "./layout";

interface Loaded {
    detail: ProcedureDetail;
    facts: Fact[];
    procedures: string[];
    articles: LinkableArticle[];
    stored: Positions;
}

/** Loads the procedure, the bot's facts and the stored positions when the creator opens, so what an agent saved meanwhile is found. */
export function CreatorScreen({ bot, id }: { bot: string; id: string }) {
    const [loaded, setLoaded] = useState<Loaded | null>(null);
    const [failure, setFailure] = useState<string | null>(null);

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        Promise.all([
            fetchProcedure(bot, id, controller.signal),
            fetchFacts(bot, controller.signal),
            fetchLayout(bot, id, controller.signal),
            fetchProcedures(bot, controller.signal),
            fetchArticles(bot, controller.signal),
        ])
            .then(([detail, facts, layout, list, articles]) => {
                setLoaded({
                    detail,
                    facts,
                    procedures: list.procedures.map((procedure) => procedure.id),
                    articles: articles.map((article) => ({ id: article.id, title: article.title })),
                    stored: Object.fromEntries(
                        Object.entries(layout.positions).map(([name, at]) => [name, { x: Number(at.x), y: Number(at.y) }]),
                    ),
                });
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [bot, id]);

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
            stored={loaded.stored}
        />
    );
}
