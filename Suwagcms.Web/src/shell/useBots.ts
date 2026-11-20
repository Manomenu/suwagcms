import { useEffect, useState } from "react";

import { fetchBots, type Bot } from "@/api/bots";

export function useBots(pathname: string): { bots: Bot[] | null; failure: string | null } {
    const [bots, setBots] = useState<Bot[] | null>(null);
    const [failure, setFailure] = useState<string | null>(null);

    useEffect(() => {
        const controller = new AbortController();
        fetchBots(controller.signal)
            .then((list) => {
                setBots(list);
                setFailure(null);
            })
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [pathname]);

    return { bots, failure };
}
