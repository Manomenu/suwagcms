import { useEffect, useState } from "react";

import { saveProcedure } from "./api";
import { toSave, type Draft } from "./draft";
import { checkOf, type Check } from "./mistakes";

const QUIET_MS = 400;

/**
 * Asks the server what saving the draft would answer, a moment after the last change: the same PUT with `dryRun`, so the list
 * can never disagree with a save. The previous answer stays on screen until the next one comes.
 */
export function useLiveCheck(bot: string, id: string, revision: number, draft: Draft): Check {
    const [check, setCheck] = useState<Check>({ state: "checking" });
    const body = JSON.stringify(toSave(draft));
    useEffect(() => {
        const controller = new AbortController();
        const timer = setTimeout(() => {
            void checkOf(saveProcedure(bot, id, JSON.parse(body) as ReturnType<typeof toSave>, revision, true, controller.signal)).then(
                (next) => {
                    if (!controller.signal.aborted) setCheck(next);
                },
            );
        }, QUIET_MS);
        return () => {
            clearTimeout(timer);
            controller.abort();
        };
    }, [bot, id, revision, body]);
    return check;
}
