"use client";

import {
  Checkbox,
  Divider,
  Grid,
  GridItem,
  Input,
  NumberDecrementStepper,
  NumberIncrementStepper,
  NumberInput,
  NumberInputField,
  NumberInputStepper,
  Text,
} from "@chakra-ui/react";
import { useState } from "react";

export default function SettingsPage() {
  return <GeneralSettings />;
}

function GeneralSettings() {
  const [allowInbound, setAllowInbound] = useState(true);

  return (
    <div
      style={{
        padding: "1em",
        flexGrow: 1,
        display: "flex",
        flexDirection: "column",
        gap: "16px",
        alignItems: "center",
      }}
    >
      <Text alignSelf={"flex-start"} fontSize={30}>
        Settings
      </Text>
      <Divider />
      <SectionHeader name="Files" />
      <LabeledInput label="Download Path" defaultValue="/app/data/downloads" />
      <LabeledInput label="Completed Path" defaultValue="/app/data/completed" />
      <Divider />
      <SectionHeader name="Connections" />

      <LabeledNumberInput
        label={"Max connections (per torrent)"}
        min={0}
        max={10_000}
        defaultValue={50}
      />
      <LabeledNumberInput
        label={"Max connections (Global)"}
        min={0}
        max={10_000}
        defaultValue={200}
      />
      <LabeledNumberInput
        label={"Max Half-open connections"}
        min={0}
        max={10_000}
        defaultValue={50}
      />

      <Divider />
      <SectionHeader name="Inbound Connections" />
      <Checkbox
        alignSelf={"flex-start"}
        defaultChecked={allowInbound}
        onChange={(e) => setAllowInbound(e.target.checked)}
      >
        Allow inbound connections
      </Checkbox>
      <LabeledNumberInput
        disabled={!allowInbound}
        label="Port"
        defaultValue={53123}
        min={0}
        max={100_000}
      />
    </div>
  );
}

interface LabeledInputProps<T> {
  label: string;
  defaultValue: T;
  disabled?: boolean;
}

interface LabeledNumberInputProps extends LabeledInputProps<number> {
  min: number;
  max: number;
}

interface SectionHeaderProps {
  name: string;
}
function SectionHeader({ name }: SectionHeaderProps) {
  return (
    <Text alignSelf={"flex-start"} fontSize={20} fontWeight={500} pb={4}>
      {name}
    </Text>
  );
}

function LabeledNumberInput({
  label,
  min,
  max,
  defaultValue,
  disabled,
}: LabeledNumberInputProps) {
  return (
    <Grid templateColumns="repeat(2, 1fr)" alignItems={"center"} gap={8}>
      <GridItem>
        <Text align={"right"}>{label}</Text>
      </GridItem>
      <NumberInput
        width={40}
        min={min}
        defaultValue={defaultValue}
        max={max}
        isDisabled={disabled}
      >
        <NumberInputField />
        <NumberInputStepper>
          <NumberIncrementStepper />
          <NumberDecrementStepper />
        </NumberInputStepper>
      </NumberInput>
    </Grid>
  );
}

function LabeledInput({
  label,
  defaultValue,
  disabled,
}: LabeledInputProps<string>) {
  return (
    <Grid templateColumns="repeat(2, 1fr)" alignItems={"center"} gap={8}>
      <GridItem>
        <Text align={"right"}>{label}</Text>
      </GridItem>
      <Input isDisabled={disabled === true} defaultValue={defaultValue} />
    </Grid>
  );
}
