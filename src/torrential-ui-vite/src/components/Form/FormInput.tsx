import { Input } from "@/components/ui/input";
import { Control, Controller, FieldValues, Path } from "react-hook-form";

interface FormInputProps<TFieldValues extends FieldValues> {
  className?: string;
  fieldName: Path<TFieldValues>;
  control: Control<TFieldValues>;
}

export const FormInput = <TFieldValues extends FieldValues>({
  className,
  fieldName,
  control,
}: FormInputProps<TFieldValues>) => {
  return (
    <Controller
      name={fieldName}
      control={control}
      render={({ field }) => <Input {...field} className={className} />}
    />
  );
};
