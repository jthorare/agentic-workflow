import type { AppState } from "../app.js";

export type CommandId = "new-tab" | "close-tab";

export interface Command {
  id: CommandId;
  title: string;
  run(state: AppState): AppState;
}

const commands = new Map<CommandId, Command>();

export function register(command: Command): void {
  commands.set(command.id, command);
}

export function runCommand(id: CommandId, state: AppState): AppState {
  const command = commands.get(id);
  if (command === undefined) {
    throw new Error(`unknown command: ${id}`);
  }
  return command.run(state);
}

export function listCommands(): Command[] {
  return [...commands.values()];
}
