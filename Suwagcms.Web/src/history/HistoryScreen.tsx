import { Anchor, Paper } from "@maniumek/suwfrontkit";
import { Link, useSearchParams } from "wouter";

import { historyHref, isHistoryCollection, useBotHistory, useResourceHistory, type HistoryCollection } from "@/api/history";
import { ChangeList } from "@/design/changeList";
import { Page } from "@/design/page";

function BotHistory({ bot }: { bot: string }) {
    const history = useBotHistory(bot);
    return (
        <Page title={`History of ${bot}`}>
            <Paper>
                <ChangeList {...history} hrefOf={(line) => historyHref(bot, line)} />
            </Paper>
        </Page>
    );
}

function ResourceHistory({ bot, collection, id }: { bot: string; collection: HistoryCollection; id: string }) {
    const history = useResourceHistory(bot, collection, id, 0, "page");
    return (
        <Page
            title={`History of ${id}`}
            actions={
                <Anchor asChild>
                    <Link href={`~/bots/${encodeURIComponent(bot)}/history`}>All changes</Link>
                </Anchor>
            }
        >
            <Paper>
                <ChangeList {...history} />
            </Paper>
        </Page>
    );
}

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
