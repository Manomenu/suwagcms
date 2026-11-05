import { Redirect, Route, Switch } from "wouter";

import { ArticlesScreen } from "./articles/ArticlesScreen";
import { BotsRoutes } from "./bots/BotsRoutes";
import { FactsScreen } from "./facts/FactsScreen";
import { KnowledgeBasesScreen } from "./knowledge-bases/KnowledgeBasesScreen";
import { CreatorScreen } from "./procedures/CreatorScreen";
import { ProceduresScreen } from "./procedures/ProceduresScreen";

/** Puts the page together from features. */
export function App() {
    return (
        <Switch>
            <Route path="/bots/:id/articles">{(params) => <ArticlesScreen key={params.id} bot={decodeURIComponent(params.id)} />}</Route>
            <Route path="/bots/:id/facts">{(params) => <FactsScreen key={params.id} bot={decodeURIComponent(params.id)} />}</Route>
            <Route path="/bots/:id/knowledge-bases">
                {(params) => <KnowledgeBasesScreen key={params.id} bot={decodeURIComponent(params.id)} />}
            </Route>
            <Route path="/bots/:id/procedures">
                {(params) => <ProceduresScreen key={params.id} bot={decodeURIComponent(params.id)} />}
            </Route>
            <Route path="/bots/:id/procedures/:procedure">
                {(params) => (
                    <CreatorScreen
                        key={`${params.id}/${params.procedure}`}
                        bot={decodeURIComponent(params.id)}
                        id={decodeURIComponent(params.procedure)}
                    />
                )}
            </Route>
            <Route path="/bots" nest>
                <BotsRoutes />
            </Route>
            <Route>
                <Redirect to="/bots" replace />
            </Route>
        </Switch>
    );
}
