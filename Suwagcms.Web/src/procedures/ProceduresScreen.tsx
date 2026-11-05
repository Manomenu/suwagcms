import { useEffect, useState } from "react";

import { Alert } from "@/design/alert";

import { fetchProcedures, type ProcedureList } from "./api";
import { ProceduresView } from "./ProceduresView";

/** Loads the procedures of the bot in the address when it opens, so procedures saved meanwhile from the CLI are found. */
export function ProceduresScreen({ bot }: { bot: string }) {
    const [list, setList] = useState<ProcedureList | null>(null);
    const [failure, setFailure] = useState<string | null>(null);

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        fetchProcedures(bot, controller.signal)
            .then(setList)
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [bot]);

    if (failure !== null) {
        return (
            <main className="mx-auto max-w-2xl p-4">
                <Alert variant="destructive">{failure}</Alert>
            </main>
        );
    }
    if (list === null) return <main className="mx-auto max-w-2xl p-4 text-muted-foreground">Loading procedures…</main>;
    return <ProceduresView bot={bot} list={list} />;
}
