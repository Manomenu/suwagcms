import { ApiError, dataOf } from "@maniumek/suwfrontkit";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { api, mistakesOf } from "./client";
import type { components } from "./openapi";

type AvailabilityView = components["schemas"]["AvailabilityView"];
type AvailabilityDelivery = components["schemas"]["AvailabilityDelivery"];

type AvailabilityKind = "article" | "procedure" | "action";

type Change = { kind: "turn"; resource: AvailabilityKind; id: string; off: boolean } | { kind: "send" };

function messageOf(error: unknown): string {
    if (error instanceof ApiError && mistakesOf(error).length > 0) {
        return mistakesOf(error)
            .map((mistake) => `${mistake.message} ${mistake.fix}`)
            .join(" ");
    }
    return error instanceof Error ? error.message : String(error);
}

function read(bot: string, ask: boolean, signal?: AbortSignal): Promise<AvailabilityView> {
    return dataOf(api.GET("/api/bots/{bot}/availability", { params: { path: { bot }, query: { ask } }, signal: signal ?? null }));
}

function deliveryOf(bot: string, change: Change): Promise<AvailabilityDelivery> {
    if (change.kind === "send") return dataOf(api.POST("/api/bots/{bot}/availability/send", { params: { path: { bot } } }));
    const params = { path: { bot, kind: change.resource, id: change.id } };
    return dataOf(
        change.off
            ? api.POST("/api/bots/{bot}/availability/{kind}/{id}/off", { params })
            : api.POST("/api/bots/{bot}/availability/{kind}/{id}/on", { params }),
    ).then((answer) => answer.delivery);
}

function availabilityKey(bot: string): unknown[] {
    return ["availability", bot];
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
    const queries = useQueryClient();
    const key = availabilityKey(bot);
    const asked = useQuery({ queryKey: key, queryFn: ({ signal }) => read(bot, true, signal) });
    const keepDelivery = (view: AvailabilityView) => {
        queries.setQueryData<AvailabilityView>(key, (was) => ({ ...view, delivery: was?.delivery ?? view.delivery }));
    };
    const reread = useMutation({ mutationFn: () => read(bot, false), onSuccess: keepDelivery });
    const change = useMutation({
        mutationFn: (wanted: Change) => deliveryOf(bot, wanted),
        onMutate: () => {
            reread.reset();
        },
        onSuccess: (delivery, wanted) => {
            queries.setQueryData<AvailabilityView>(key, (was) => (was === undefined ? was : { ...was, delivery }));
            if (wanted.kind === "turn") reread.mutate();
        },
    });

    const view = asked.data ?? null;
    const failed = change.error ?? reread.error ?? asked.error;
    return {
        delivery: view?.delivery ?? null,
        failure: failed === null ? null : messageOf(failed),
        isOff: (kind, id) => view?.resources.some((item) => item.kind === kind && item.id === id && item.off) ?? false,
        turn: (resource, id, off) => {
            change.mutate({ kind: "turn", resource, id, off });
        },
        send: () => {
            change.mutate({ kind: "send" });
        },
        reload: () => {
            reread.mutate();
        },
    };
}
