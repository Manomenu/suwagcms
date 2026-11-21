import { useEffect, useState, type ReactNode } from "react";
import { Anchor, ErrorBoundary as Boundary, ErrorScreen, type Failure } from "@maniumek/suwfrontkit";
import { useLocation } from "wouter";

function ScreenFailure({ error, retry }: Failure) {
    const [pathname] = useLocation();
    const [failedAt] = useState(pathname);
    useEffect(() => {
        if (pathname !== failedAt) retry();
    }, [pathname, failedAt, retry]);
    return (
        <ErrorScreen
            error={error}
            title="This screen failed to open."
            actions={
                <>
                    <Anchor href={window.location.href}>Reload</Anchor>
                    <Anchor href="/bots">← Back to the bots</Anchor>
                </>
            }
        />
    );
}

export function ErrorBoundary({ children }: { children: ReactNode }) {
    return <Boundary fallback={(failure) => <ScreenFailure {...failure} />}>{children}</Boundary>;
}
