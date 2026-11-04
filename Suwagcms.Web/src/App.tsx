import { Redirect, Route, Switch } from "wouter";

import { BotsRoutes } from "./bots/BotsRoutes";
import { FactsScreen } from "./facts/FactsScreen";

/** Puts the page together from features. */
export function App() {
    return (
        <Switch>
            <Route path="/bots/:id/facts">{(params) => <FactsScreen key={params.id} bot={decodeURIComponent(params.id)} />}</Route>
            <Route path="/bots" nest>
                <BotsRoutes />
            </Route>
            <Route>
                <Redirect to="/bots" replace />
            </Route>
        </Switch>
    );
}
