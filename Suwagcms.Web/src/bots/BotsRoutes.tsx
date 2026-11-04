import { Route, Switch } from "wouter";

import { BotsScreen } from "./BotsScreen";

/** The routes under `/bots`: the list, and one bot's profile at `/bots/<id>`. */
export function BotsRoutes() {
    return (
        <Switch>
            <Route path="/">
                <BotsScreen key="list" openId={null} />
            </Route>
            <Route path="/:id">{(params) => <BotsScreen key={params.id} openId={decodeURIComponent(params.id)} />}</Route>
        </Switch>
    );
}
