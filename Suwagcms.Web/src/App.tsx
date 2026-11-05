import { Redirect, Route, Switch } from "wouter";

import { ActionsScreen } from "./actions/ActionsScreen";
import { ArticlesScreen } from "./articles/ArticlesScreen";
import { BotsRoutes } from "./bots/BotsRoutes";
import { FactsScreen } from "./facts/FactsScreen";
import { HistoryScreen } from "./history/HistoryScreen";
import { KnowledgeBasesScreen } from "./knowledge-bases/KnowledgeBasesScreen";
import { PersonaScreen } from "./persona/PersonaScreen";
import { CreatorScreen } from "./procedures/CreatorScreen";
import { ProceduresScreen } from "./procedures/ProceduresScreen";
import { AppShell } from "./shell/AppShell";
import { VersionsScreen } from "./versions/VersionsScreen";

/** Puts the page together from features. */
export function App() {
    return (
        <AppShell>
            <Switch>
                <Route path="/bots/:id/articles">
                    {(params) => <ArticlesScreen key={params.id} bot={decodeURIComponent(params.id)} />}
                </Route>
                <Route path="/bots/:id/facts">{(params) => <FactsScreen key={params.id} bot={decodeURIComponent(params.id)} />}</Route>
                <Route path="/bots/:id/knowledge-bases">
                    {(params) => <KnowledgeBasesScreen key={params.id} bot={decodeURIComponent(params.id)} />}
                </Route>
                <Route path="/bots/:id/procedures">
                    {(params) => <ProceduresScreen key={params.id} bot={decodeURIComponent(params.id)} />}
                </Route>
                <Route path="/bots/:id/actions">{(params) => <ActionsScreen key={params.id} bot={decodeURIComponent(params.id)} />}</Route>
                <Route path="/bots/:id/persona">{(params) => <PersonaScreen key={params.id} bot={decodeURIComponent(params.id)} />}</Route>
                <Route path="/bots/:id/versions">
                    {(params) => <VersionsScreen key={params.id} bot={decodeURIComponent(params.id)} />}
                </Route>
                <Route path="/bots/:id/history">{(params) => <HistoryScreen key={params.id} bot={decodeURIComponent(params.id)} />}</Route>
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
        </AppShell>
    );
}
