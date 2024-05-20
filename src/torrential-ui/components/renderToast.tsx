import React from "react";
import { Box, ToastId } from "@chakra-ui/react";
import { ToastNotification } from "@/features/notificationsSlice";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import styles from "./ToastNotification.module.css";
import { type IconDefinition } from "@fortawesome/free-solid-svg-icons";

export const renderToast = (
  id: ToastId,
  onClose: () => void,
  notification: ToastNotification
) => {
  const { status, icon, title, description } = notification;
  let bgColor = "gray.300";
  if (status === "success") bgColor = "green.300";
  if (status === "warning") bgColor = "orange.300";
  if (status === "error") bgColor = "red.300";
  return (
    <Box bg={bgColor} id={id.toString()} className={styles.toastContainer}>
      <Box className={styles.icon}>
        {typeof icon !== "string" && icon !== undefined && (
          <FontAwesomeIcon icon={icon as IconDefinition} size={"lg"} />
        )}
      </Box>
      <Box className={styles.content}>
        <Box className={styles.title}>
          <Text>{title}</Text>
        </Box>
        <Box className={styles.description}></Box>
      </Box>
    </Box>
  );
};
