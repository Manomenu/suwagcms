import { ApiError } from "@maniumek/suwfrontkit";
import { QueryClient } from "@tanstack/react-query";

const MOST_RETRIES = 2;

export function retriesOnlyWhatMayPass(failures: number, error: Error): boolean {
    const refused = error instanceof ApiError && error.status >= 400 && error.status < 500;
    return !refused && failures < MOST_RETRIES;
}

export function createQueryClient(): QueryClient {
    return new QueryClient({ defaultOptions: { queries: { retry: retriesOnlyWhatMayPass } } });
}
