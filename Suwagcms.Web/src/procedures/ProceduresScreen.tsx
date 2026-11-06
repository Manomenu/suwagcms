import { useEffect, useState } from "react";
import { useLocation } from "wouter";

import { useAvailability } from "@/api/availability";
import { addressOf, editHref, listHref, newHref } from "@/api/resourceAddress";
import { Alert } from "@/design/alert";
import { AvailabilityNotice } from "@/design/availabilityPanel";
import { Page } from "@/design/page";
import { BackToList } from "@/design/resourceEdit";

import { fetchProcedures, type ProcedureList } from "./api";
import { NewProcedureForm } from "./NewProcedureForm";
import { ProceduresView } from "./ProceduresView";

/** Loads the procedures of the bot in the address when it opens, so procedures saved meanwhile from the CLI are found. */
export function ProceduresScreen({ bot }: { bot: string }) {
    const [list, setList] = useState<ProcedureList | null>(null);
    const [failure, setFailure] = useState<string | null>(null);
    const [location, navigate] = useLocation();
    const availability = useAvailability(bot);

    useEffect(() => {
        // StrictMode runs effects twice in development: abort the first request, ignore its result.
        const controller = new AbortController();
        fetchProcedures(bot, controller.signal)
            .then(setList)
            .catch((error: unknown) => {
                if (!controller.signal.aborted) setFailure(error instanceof Error ? error.message : String(error));
            });
        return () => {
            controller.abort();
        };
    }, [bot]);

    if (failure !== null) {
        return <Alert variant="destructive">{failure}</Alert>;
    }
    const backToList = () => {
        navigate(listHref(bot, "procedures"), { replace: true });
    };
    if (addressOf(location, "procedures").screen === "new") {
        return (
            <div className="flex flex-col gap-4">
                <BackToList plural="procedures" onBack={backToList} />
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
            list={list}
            isOff={(id) => availability.isOff("procedure", id)}
            notice={<AvailabilityNotice delivery={availability.delivery} failure={availability.failure} onSend={availability.send} />}
            onAdd={() => {
                navigate(newHref(bot, "procedures"));
            }}
            onOpen={(id) => {
                navigate(editHref(bot, "procedures", id));
            }}
        />
    );
}
