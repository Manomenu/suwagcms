import { retryUnlessRefused } from "@maniumek/suwfrontkit";
import { QueryClient } from "@tanstack/react-query";

export function createQueryClient(): QueryClient {
    return new QueryClient({ defaultOptions: { queries: { retry: retryUnlessRefused } } });
}
