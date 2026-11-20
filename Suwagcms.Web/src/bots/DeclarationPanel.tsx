import { useCallback, useEffect, useState } from "react";

import { fetchDeclaration, type DeclarationStatus } from "@/api/declaration";

import { DeclarationView } from "./DeclarationView";

export function DeclarationPanel({ bot }: { bot: string }) {
    const [status, setStatus] = useState<DeclarationStatus | null>(null);
    const [asking, setAsking] = useState(true);
    const [failure, setFailure] = useState<string | null>(null);

    const ask = useCallback(
        (signal: AbortSignal) => {
            fetchDeclaration(bot, signal)
                .then(setStatus)
                .catch((error: unknown) => {
                    if (!signal.aborted) setFailure(error instanceof Error ? error.message : String(error));
                })
                .finally(() => {
                    if (!signal.aborted) setAsking(false);
                });
        },
        [bot],
    );

    useEffect(() => {
        const controller = new AbortController();
        ask(controller.signal);
        return () => {
            controller.abort();
        };
    }, [ask]);

    return (
        <DeclarationView
            status={status}
            asking={asking}
            failure={failure}
            onAsk={() => {
                setAsking(true);
                setFailure(null);
                ask(new AbortController().signal);
            }}
        />
    );
}
