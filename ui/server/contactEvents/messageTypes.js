import { readFileSync } from "node:fs";
import path from "node:path";
import { repositoryRoot } from "../shared/paths.js";

const serviceBusConfigPath = path.resolve(repositoryRoot, "infra", "servicebus", "config.json");
const contractsPath = path.resolve(repositoryRoot, "contracts");
const serviceBusConfig = JSON.parse(readFileSync(serviceBusConfigPath, "utf8"));
export const subscriberLabels = serviceBusConfig.Dashboard?.SubscriberLabels ?? {};
export const producerLabels = serviceBusConfig.Dashboard?.ProducerLabels ?? {};
const configuredMessageTypes = serviceBusConfig.Dashboard?.MessageTypes ?? {};

export function getMessageTypes() {
  return Object.fromEntries(Object.entries(configuredMessageTypes).map(([type, configuration]) => {
    const schema = JSON.parse(readFileSync(path.join(contractsPath, configuration.Schema), "utf8"));
    const configurationErrors = [];
    const formFields = configuration.FormFields ?? {};
    for (const [name, field] of Object.entries(formFields)) {
      if (!schema.properties?.[name] && !field.Type) {
        configurationErrors.push(`Configured field "${name}" was not found in the contract schema.`);
      }
      if (!field.Label || field.Label.trim() === "") {
        configurationErrors.push(`No label was configured for field "${name}".`);
      }
    }
    const schemaFields = Object.entries(schema.properties ?? {}).map(([name, property]) => ({
      Name: name,
      Type: property.type === "boolean" ? "boolean" : property.format === "email" ? "email" : property.type === "object" ? "object" : "text",
      Required: (schema.required ?? []).includes(name),
      Options: property.enum,
    }));
    const additionalFields = Object.entries(formFields)
      .filter(([name]) => !schema.properties?.[name])
      .map(([name, field]) => ({ Name: name, ...field }));
    const fieldsByName = new Map([...schemaFields, ...additionalFields].map((field) => [field.Name, field]));
    const order = [...fieldsByName.keys()].sort((left, right) =>
      (formFields[left]?.Order ?? Number.MAX_SAFE_INTEGER) -
      (formFields[right]?.Order ?? Number.MAX_SAFE_INTEGER));
    const fields = order
      .map((name) => fieldsByName.get(name))
      .filter(Boolean)
      .map((field) => ({ ...field, Label: formFields[field.Name]?.Label ?? field.Name }));
    return [type, { DisplayName: configuration.DisplayName, Fields: fields, ConfigurationErrors: configurationErrors }];
  }));
}

export const messageTypes = getMessageTypes();
