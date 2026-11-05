// Why: the article, procedure and action screens turn resources on and off through the same routes and read the same setting, so the calls and their hook exist once.
import { useCallback, useEffect, useState } from "react";

import { ApiError, request } from "./client";
import type { components } from "./openapi";

type AvailabilityView = components["schemas"]["AvailabilityView"];
type AvailabilityAnswer = components["schemas"]["AvailabilityAnswer"];
type AvailabilityDelivery = components["schemas"]["AvailabilityDelivery"];

type AvailabilityKind = "article" | "procedure" | "action";

function botPath(bot: string): string {
    return `/bots/${encodeURIComponent(bot)}/availability`;
}

function messageOf(error: unknown): string {
    if (error instanceof ApiError && error.mistakes.length > 0) {
        return error.mistakes.map((mistake) => `${mistake.message} ${mistake.fix}`).join(" ");
    }
    return error instanceof Error ? error.message : String(error);
}

export interface AvailabilityState {
    delivery: AvailabilityDelivery | null;
    failure: string | null;
    isOff: (kind: AvailabilityKind, id: string) => boolean;
    turn: (kind: AvailabilityKind, id: string, off: boolean) => void;
    send: () => void;
    reload: () => void;
}

/** What a bot has on and off, as the CMS holds it, and whether the bot has it, from the bot's own answer; asking the bot does not hold up the rest of the screen. */
export function useAvailability(bot: string): AvailabilityState {
    const [view, setView] = useState<AvailabilityView | null>(null);
    const [delivery, setDelivery] = useState<AvailabilityDelivery | null>(null);
    const [failure, setFailure] = useState<string | null>(null);

    const load = useCallback(
        (ask: boolean, signal: AbortSignal) =>
            request<AvailabilityView>(`${botPath(bot)}?ask=${ask ? "true" : "false"}`, { signal }).then((answer) => {
                setView(answer);
                if (ask) setDelivery(answer.delivery);
            }),
        [bot],
    );

    useEffect(() => {
        const controller = new AbortController();
        load(true, controller.signal).catch((error: unknown) => {
            if (!controller.signal.aborted) setFailure(messageOf(error));
        });
        return () => {
            controller.abort();
        };
    }, [load]);

    const turn = useCallback(
        (kind: AvailabilityKind, id: string, off: boolean) => {
            setFailure(null);
            const path = `${botPath(bot)}/${kind}/${encodeURIComponent(id)}/${off ? "off" : "on"}`;
            request<AvailabilityAnswer>(path, { method: "POST" })
                .then((answer) => {
                    setDelivery(answer.delivery);
                    return load(false, new AbortController().signal);
                })
                .catch((error: unknown) => {
                    setFailure(messageOf(error));
                });
        },
        [bot, load],
    );

    const send = useCallback(() => {
        setFailure(null);
        request<AvailabilityDelivery>(`${botPath(bot)}/send`, { method: "POST" })
            .then(setDelivery)
            .catch((error: unknown) => {
                setFailure(messageOf(error));
            });
    }, [bot]);

    const isOff = useCallback(
        (kind: AvailabilityKind, id: string) => view?.resources.some((item) => item.kind === kind && item.id === id && item.off) ?? false,
        [view],
    );

    const reload = useCallback(() => {
        load(false, new AbortController().signal).catch((error: unknown) => {
            setFailure(messageOf(error));
        });
    }, [load]);

    return { delivery, failure, isOff, turn, send, reload };
}
