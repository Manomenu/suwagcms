import type { ComponentProps } from "react";
import { GuardedLink } from "@maniumek/suwfrontkit";
import { useLocation } from "wouter";

export function AppLink(props: Omit<ComponentProps<typeof GuardedLink>, "navigate">) {
    const [, navigate] = useLocation();
    return (
        <GuardedLink
            {...props}
            navigate={(href) => {
                navigate(href);
            }}
        />
    );
}
