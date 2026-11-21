import { Alert } from "@maniumek/suwfrontkit";
import { useQuery } from "@tanstack/react-query";
import { useLocation, useSearch } from "wouter";

import { useAvailability } from "@/api/availability";
import { messageOf } from "@/api/client";
import { useKindHistory } from "@/api/history";
import { addressOf, editHref, listHref, newHref, withListSearch } from "@/api/resourceAddress";
import { AvailabilityNotice } from "@/design/availabilityPanel";
import { Page } from "@/design/page";
import { BackToList } from "@/design/resourceEdit";

import { fetchProcedures } from "./api";
import { NewProcedureForm } from "./NewProcedureForm";
import { ProceduresView } from "./ProceduresView";

export function ProceduresScreen({ bot }: { bot: string }) {
    const procedures = useQuery({ queryKey: ["procedures", bot, "list"], queryFn: ({ signal }) => fetchProcedures(bot, signal) });
    const list = procedures.data ?? null;
    const failure = procedures.error === null ? null : messageOf(procedures.error);
    const [location, navigate] = useLocation();
    const search = useSearch();
    const availability = useAvailability(bot);
    const listed = addressOf(location, "procedures").screen === "list";
    const history = useKindHistory(bot, listed ? "procedures" : null);

    if (failure !== null) {
        return <Alert variant="destructive">{failure}</Alert>;
    }
    const listAddress = withListSearch(listHref(bot, "procedures"), search);
    const backToList = () => {
        navigate(listAddress, { replace: true });
    };
    if (addressOf(location, "procedures").screen === "new") {
        return (
            <div className="flex flex-col gap-4">
                <BackToList plural="procedures" backHref={listAddress} />
                <Page title="Add a procedure">
                    <NewProcedureForm bot={bot} onCancel={backToList} />
                </Page>
            </div>
        );
    }
    if (list === null) return <p className="text-muted-foreground">Loading procedures…</p>;
    return (
        <ProceduresView
            bot={bot}
            history={history}
            list={list}
            isOff={(id) => availability.isOff("procedure", id)}
            notice={<AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />}
            addHref={withListSearch(newHref(bot, "procedures"), search)}
            hrefOf={(id) => withListSearch(editHref(bot, "procedures", id), search)}
        />
    );
}
