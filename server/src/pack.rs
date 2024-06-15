use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Deserialize, Serialize, Clone)]
pub struct Pack {
    pub name: String,
    pub id: Uuid,
    #[serde(skip_serializing)]
    pub visible: bool,
    #[serde(default, skip_serializing)]
    pub order: u8,
    pub templates: Vec<Template>,
    pub conjunctions: Option<Vec<String>>,
    pub words: Option<Vec<WordList>>,
}

#[derive(Debug, Deserialize, Serialize, Clone)]
#[serde(untagged)]
pub enum Template {
    Basic(String),
    List {
        template: String,
        words: Vec<String>,
    },
}

impl Template {
    pub fn template(&self) -> &str {
        match self {
            Self::Basic(template) => template,
            Self::List { template, .. } => template,
        }
    }

    pub fn requires_word(&self) -> bool {
        self.template().contains("{0}")
    }

    pub fn format(&self, word: &str) -> String {
        self.template().replace("{0}", word)
    }
}

#[derive(Debug, Deserialize, Serialize, Clone)]
pub struct WordList {
    pub name: String,
    pub words: Vec<String>,
}

impl Pack {
    fn get_word(&self, list_idx: usize, word_idx: usize) -> Option<&str> {
        let words = self.words.as_ref()?;
        let list = words.get(list_idx)?;
        list.words
            .get(word_idx)
            .map(|word| word.as_str())
    }

    fn replace(
        &self,
        template: &Template,
        list_idx: usize,
        word_idx: usize,
    ) -> Option<String> {
        let word = match template {
            Template::Basic(_) => self.get_word(list_idx, word_idx),
            Template::List { words, .. } => words
                .get(word_idx)
                .map(|word| word.as_str()),
        }?;

        Some(template.format(word))
    }

    fn partial_format(
        &self,
        template: &Template,
        word_idx: Option<(usize, usize)>,
    ) -> Option<String> {
        let requires_word = template.requires_word();
        if requires_word && word_idx.is_none() {
            return None;
        }

        if_chain::if_chain! {
            if requires_word;
            if let Some((list_idx, word_idx)) = word_idx;
            then {
                self.replace(template, list_idx, word_idx)
            } else {
                Some(template.template().to_string())
            }
        }
    }

    pub fn format(
        &self,
        template_1_idx: usize,
        word_1_idx: Option<(usize, usize)>,
        conjunction: Option<usize>,
        template_2_idx: Option<usize>,
        word_2_idx: Option<(usize, usize)>,
    ) -> Option<String> {
        let template_1 = self.templates.get(template_1_idx)?;
        let mut formatted = self.partial_format(template_1, word_1_idx)?;

        if_chain::if_chain! {
            if let Some(conj_idx) = conjunction;
            if let Some(conjunctions) = &self.conjunctions;
            if let Some(template_2_idx) = template_2_idx;
            then {
                let template_2 = self.templates.get(template_2_idx)?;
                let append = self.partial_format(template_2, word_2_idx)?;

                let conj = conjunctions.get(conj_idx)?;
                let is_punc = conj.len() == 1 && conj.chars().next().map(|x| x.is_ascii_punctuation()).unwrap_or(false);
                if is_punc {
                    formatted.push_str(conj);
                    formatted.push('\n');
                } else {
                    formatted.push('\n');
                    formatted.push_str(conj);
                    formatted.push(' ');
                }

                formatted.push_str(&append);
            }
        }

        Some(formatted)
    }
}
