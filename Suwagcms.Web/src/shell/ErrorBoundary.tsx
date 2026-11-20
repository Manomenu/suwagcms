import { Component, type ReactNode } from "react";
import { useLocation } from "wouter";

import { Alert } from "@/design/alert";

interface Props {
    pathname: string;
    children: ReactNode;
}

interface State {
    error: Error | null;
    pathname: string;
}

class Catcher extends Component<Props, State> {
    override state: State = { error: null, pathname: this.props.pathname };

    static getDerivedStateFromError(error: Error): Partial<State> {
        return { error };
    }

    static getDerivedStateFromProps(props: Props, state: State): Partial<State> | null {
        return props.pathname === state.pathname ? null : { error: null, pathname: props.pathname };
    }

    override render() {
        const { error } = this.state;
        if (error === null) return this.props.children;
        return (
            <Alert variant="destructive" className="flex flex-col gap-2">
                <p className="font-semibold">This screen failed to open.</p>
                <p>{error.message}</p>
                <p className="flex gap-4">
                    <a className="text-anchor underline underline-offset-4" href={window.location.href}>
                        Reload
                    </a>
                    <a className="text-anchor underline underline-offset-4" href="/bots">
                        ← Back to the bots
                    </a>
                </p>
            </Alert>
        );
    }
}

export function ErrorBoundary({ children }: { children: ReactNode }) {
    const [pathname] = useLocation();
    return <Catcher pathname={pathname}>{children}</Catcher>;
}
