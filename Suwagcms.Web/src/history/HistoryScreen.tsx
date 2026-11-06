import { useSearchParams } from "wouter";

import { historyHref, isHistoryCollection, useBotHistory, useResourceHistory, type HistoryCollection } from "@/api/history";
import { Anchor } from "@/design/anchor";
import { HistoryList } from "@/design/historyPanel";
import { Page, Paper } from "@/design/page";

function BotHistory({ bot }: { bot: string }) {
    const history = useBotHistory(bot);
    return (
        <Page title={`History of ${bot}`}>
            <Paper>
                <HistoryList {...history} hrefOf={(line) => historyHref(bot, line)} />
            </Paper>
        </Page>
    );
}

function ResourceHistory({ bot, collection, id }: { bot: string; collection: HistoryCollection; id: string }) {
    const history = useResourceHistory(bot, collection, id, 0, "page");
    return (
        <Page title={`History of ${id}`} actions={<Anchor href={`~/bots/${encodeURIComponent(bot)}/history`}>All changes</Anchor>}>
            <Paper>
                <HistoryList {...history} />
            </Paper>
        </Page>
    );
}

/** Every change to the bot in the address, or with `?kind=<collection>&id=<id>` to one resource, which stays readable after it is deleted. */
export function HistoryScreen({ bot }: { bot: string }) {
    const [params] = useSearchParams();
    const collection = params.get("kind");
    const id = params.get("id");
    return isHistoryCollection(collection) && id !== null ? (
        <ResourceHistory key={`${collection}/${id}`} bot={bot} collection={collection} id={id} />
    ) : (
        <BotHistory bot={bot} />
    );
}
