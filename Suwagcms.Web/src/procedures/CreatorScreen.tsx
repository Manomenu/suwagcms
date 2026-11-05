import { useEffect, useState } from "react";

import { fetchDeclaration } from "@/api/declaration";
import { Alert } from "@/design/alert";

import { fetchFacts, fetchLayout, fetchProcedure, type Fact, type ProcedureDetail } from "./api";
import { Creator } from "./Creator";
import type { Positions } from "./layout";
import { offeredBlocks, type OfferedBlocks } from "./offeredBlocks";

interface Loaded {
    detail: ProcedureDetail;
    facts: Fact[];
    stored: Positions;
    offered: OfferedBlocks;
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
            // Why: the last answer, not a new ask: opening a form must not wait on a bot.
            fetchDeclaration(bot, controller.signal, false),
        ])
            .then(([detail, facts, layout, declaration]) => {
                setLoaded({
                    detail,
                    facts,
                    offered: offeredBlocks(declaration),
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
    return <Creator bot={bot} id={id} initial={loaded.detail} facts={loaded.facts} stored={loaded.stored} offered={loaded.offered} />;
}
