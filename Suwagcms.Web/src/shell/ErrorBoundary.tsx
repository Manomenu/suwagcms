import { useEffect, useState, type ReactNode } from "react";
import { Alert, ErrorBoundary as Boundary, type Failure } from "@maniumek/suwfrontkit";
import { useLocation } from "wouter";

function ScreenFailure({ error, retry }: Failure) {
    const [pathname] = useLocation();
    const [failedAt] = useState(pathname);
    useEffect(() => {
        if (pathname !== failedAt) retry();
    }, [pathname, failedAt, retry]);
    return (
        <Alert variant="destructive" className="flex flex-col gap-2">
            <p className="font-semibold">This screen failed to open.</p>
            <p>{error instanceof Error ? error.message : String(error)}</p>
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

export function ErrorBoundary({ children }: { children: ReactNode }) {
    return <Boundary fallback={(failure) => <ScreenFailure {...failure} />}>{children}</Boundary>;
}
