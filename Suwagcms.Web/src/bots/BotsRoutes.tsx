import { Route, Switch } from "wouter";

import { BotsScreen } from "./BotsScreen";

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
