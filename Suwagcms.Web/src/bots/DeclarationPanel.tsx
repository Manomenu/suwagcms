import { useQuery } from "@tanstack/react-query";

import { messageOf } from "@/api/client";
import { fetchDeclaration } from "@/api/declaration";

import { DeclarationView } from "./DeclarationView";

export function DeclarationPanel({ bot }: { bot: string }) {
    const declaration = useQuery({ queryKey: ["declaration", bot], queryFn: ({ signal }) => fetchDeclaration(bot, signal) });

    return (
        <DeclarationView
            status={declaration.data ?? null}
            asking={declaration.isFetching}
            failure={declaration.error === null || declaration.isFetching ? null : messageOf(declaration.error)}
            onAsk={() => {
                void declaration.refetch();
            }}
        />
    );
}
