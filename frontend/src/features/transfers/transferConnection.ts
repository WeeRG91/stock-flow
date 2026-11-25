import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

export function createTransferConnection() {
    return new HubConnectionBuilder()
    .withUrl("/hubs/transfers")
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}