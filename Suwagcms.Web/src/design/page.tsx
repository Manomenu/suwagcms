import * as React from "react";
import { cn } from "@/design/classNames";

export function Page({ title, actions, children }: { title: string; actions?: React.ReactNode; children?: React.ReactNode }) {
    return (
        <div className="flex flex-col gap-4">
            <div className="flex items-center justify-between gap-4">
                <h1 className="text-[26px] leading-[1.35] font-bold">{title}</h1>
                {actions !== undefined && <div className="flex items-center gap-2">{actions}</div>}
            </div>
            {children}
        </div>
    );
}

export function Paper({ className, ...props }: React.ComponentProps<"div">) {
    return <div className={cn("rounded-lg border border-border bg-card p-4", className)} {...props} />;
}
