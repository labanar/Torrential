import {
  NumberDecrementStepper,
  NumberIncrementStepper,
  NumberInput,
  NumberInputField,
  NumberInputStepper,
} from "@chakra-ui/react";
import { Control, Controller } from "react-hook-form";

interface FormNumericInputProps {
  min?: number;
  max?: number;
  className?: string;
  fieldName: string;
  control: Control<any, any>;
}

export const FormNumericInput: React.FC<FormNumericInputProps> = ({
  min,
  max,
  className,
  fieldName,
  control,
}) => {
  return (
    <Controller
      name={fieldName}
      control={control}
      render={({ field }) => (
        <NumberInput className={className} min={min} max={max} {...field}>
          <NumberInputField />
          <NumberInputStepper>
            <NumberIncrementStepper />
            <NumberDecrementStepper />
          </NumberInputStepper>
        </NumberInput>
      )}
    />
  );
};
