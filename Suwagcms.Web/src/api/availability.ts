import { ApiError, dataOf } from "@maniumek/suwfrontkit";
import { useCallback, useEffect, useState } from "react";

import { api, mistakesOf } from "./client";
import type { components } from "./openapi";

type AvailabilityView = components["schemas"]["AvailabilityView"];
type AvailabilityDelivery = components["schemas"]["AvailabilityDelivery"];

type AvailabilityKind = "article" | "procedure" | "action";

function messageOf(error: unknown): string {
    if (error instanceof ApiError && mistakesOf(error).length > 0) {
        return mistakesOf(error)
            .map((mistake) => `${mistake.message} ${mistake.fix}`)
            .join(" ");
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

export function useAvailability(bot: string): AvailabilityState {
    const [view, setView] = useState<AvailabilityView | null>(null);
    const [delivery, setDelivery] = useState<AvailabilityDelivery | null>(null);
    const [failure, setFailure] = useState<string | null>(null);

    const load = useCallback(
        (ask: boolean, signal: AbortSignal) =>
            dataOf(api.GET("/api/bots/{bot}/availability", { params: { path: { bot }, query: { ask } }, signal })).then((answer) => {
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

    const turn = (kind: AvailabilityKind, id: string, off: boolean) => {
        setFailure(null);
        const params = { path: { bot, kind, id } };
        dataOf(
            off
                ? api.POST("/api/bots/{bot}/availability/{kind}/{id}/off", { params })
                : api.POST("/api/bots/{bot}/availability/{kind}/{id}/on", { params }),
        )
            .then((answer) => {
                setDelivery(answer.delivery);
                return load(false, new AbortController().signal);
            })
            .catch((error: unknown) => {
                setFailure(messageOf(error));
            });
    };

    const send = () => {
        setFailure(null);
        dataOf(api.POST("/api/bots/{bot}/availability/send", { params: { path: { bot } } }))
            .then(setDelivery)
            .catch((error: unknown) => {
                setFailure(messageOf(error));
            });
    };

    const isOff = (kind: AvailabilityKind, id: string) =>
        view?.resources.some((item) => item.kind === kind && item.id === id && item.off) ?? false;

    const reload = () => {
        load(false, new AbortController().signal).catch((error: unknown) => {
            setFailure(messageOf(error));
        });
    };

    return { delivery, failure, isOff, turn, send, reload };
}
