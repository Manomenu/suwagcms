import { Redirect, Route, Switch } from "wouter";

import { BotsRoutes } from "./bots/BotsRoutes";

/** Puts the page together from features. */
export function App() {
    return (
        <Switch>
            <Route path="/bots" nest>
                <BotsRoutes />
            </Route>
            <Route>
                <Redirect to="/bots" replace />
            </Route>
        </Switch>
    );
}
