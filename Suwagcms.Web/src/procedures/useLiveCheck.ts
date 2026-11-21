import { keepPreviousData, skipToken, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";

import { procedureKey, saveProcedure } from "./api";
import { toSave, type Draft } from "./draft";
import { checkOf, type Check } from "./mistakes";

const QUIET_MS = 400;

function useQuiet(text: string): string | null {
    const [quiet, setQuiet] = useState<string | null>(null);
    useEffect(() => {
        const timer = setTimeout(() => {
            setQuiet(text);
        }, QUIET_MS);
        return () => {
            clearTimeout(timer);
        };
    }, [text]);
    return quiet;
}

export function useLiveCheck(bot: string, id: string, revision: number, draft: Draft): Check {
    const body = useQuiet(JSON.stringify(toSave(draft)));
    const check = useQuery({
        queryKey: [...procedureKey(bot, id), "check", revision, body],
        queryFn:
            body === null
                ? skipToken
                : ({ signal }) => checkOf(saveProcedure(bot, id, JSON.parse(body) as ReturnType<typeof toSave>, revision, true, signal)),
        placeholderData: keepPreviousData,
    });
    return check.data ?? { state: "checking" };
}
