import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

export function createInventoryConnection() {
  return new HubConnectionBuilder()
    .withUrl("/hubs/inventory")
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();
}
