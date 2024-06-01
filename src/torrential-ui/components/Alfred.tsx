import {
  Divider,
  Input,
  Modal,
  ModalBody,
  ModalContent,
  ModalOverlay,
  Text,
} from "@chakra-ui/react";
import {
  IconDefinition,
  faGear,
  faPeopleGroup,
  faUpDown,
} from "@fortawesome/free-solid-svg-icons";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import classNames from "classnames";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useHotkeys, useHotkeysContext } from "react-hotkeys-hook";
import styles from "./Aldred.module.css";

interface AlfredProps {
  isOpen: boolean;
  close: () => void;
}

export default function Alfred({ isOpen, close }: AlfredProps) {
  const { enableScope, disableScope } = useHotkeysContext();
  const router = useRouter();

  const [selectedId, setSelectedId] = useState(0);

  useEffect(() => {
    if (isOpen) {
      enableScope("search");
    } else {
      disableScope("search");
      setSelectedId(-1);
    }
    return () => disableScope("search"); // Clean up on unmount
  }, [isOpen]);

  useHotkeys(
    "up",
    () => {
      let nextId = selectedId - 1;
      if (nextId < 0) nextId = 2;
      setSelectedId(nextId);
      console.log(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "down",
    () => {
      let nextId = selectedId + 1;
      if (nextId > 2) nextId = nextId - 3;
      setSelectedId(nextId);
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  useHotkeys(
    "enter",
    () => {
      switch (selectedId) {
        case 0:
          router.push("/peers");
          close();
          break;
        case 1:
          router.push("/settings");
          close();
          break;
        case 2:
          router.push("/");
          close();
          break;
      }
    },
    {
      scopes: ["search"],
      enableOnFormTags: ["input", "textarea", "select"],
    }
  );

  return (
    <Modal isOpen={isOpen} onClose={() => {}} size={"xl"}>
      <ModalOverlay bg={"none"} />
      <ModalContent mt={"16%"}>
        <div>
          <Input
            placeholder="Type to search"
            border={0}
            height={14}
            focusBorderColor="none"
            _focus={{ boxShadow: "none" }}
            onKeyDown={(e) => {
              if (e.key === "Escape") {
                e.preventDefault();
              }
            }}
          />
        </div>
        <Divider />

        <ModalBody p={0}>
          <SearchSuggestion
            selected={selectedId == 0}
            icon={faPeopleGroup}
            title="Peers"
          />
          <SearchSuggestion
            selected={selectedId == 1}
            icon={faGear}
            title="Settings"
          />
          <SearchSuggestion
            selected={selectedId == 2}
            icon={faUpDown}
            title="Torrents"
          />
        </ModalBody>
      </ModalContent>
    </Modal>
  );
}

interface SearchSuggestionProps {
  selected: boolean;
  icon?: IconDefinition;
  title: string;
}

function SearchSuggestion({ selected, icon, title }: SearchSuggestionProps) {
  return (
    <>
      <div
        className={classNames(styles.suggestion, {
          [styles.selected]: selected,
        })}
      >
        <FontAwesomeIcon icon={icon} size="xl" width={"28px"} />
        <Text fontSize={14}>{title}</Text>
      </div>
    </>
  );
}

interface SearchSuggestion {
  context: string;
  icon?: IconDefinition;
  title: string;
  action: () => void;
}
