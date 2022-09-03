use serde::Deserialize;
use uuid::Uuid;

#[derive(Debug, Deserialize)]
pub struct Pack {
    pub name: String,
    pub id: Uuid,
    pub templates: Vec<String>,
    pub conjunctions: Vec<String>,
    pub words: Vec<WordList>,
}

#[derive(Debug, Deserialize)]
pub struct WordList {
    pub name: String,
    pub words: Vec<String>,
}

impl Pack {
    pub fn format(&self, template_1_idx: usize, word_1_idx: Option<(usize, usize)>, conjunction: Option<usize>, template_2_idx: Option<usize>, word_2_idx: Option<(usize, usize)>) -> Option<String> {
        let template_1 = self.templates.get(template_1_idx)?;

        if template_1.contains("{0}") && word_1_idx.is_none() {
            return None;
        }

        let mut formatted = if template_1.contains("{0}") && let Some((w1_list, w1_word)) = word_1_idx {
            let word_1 = self.words.get(w1_list)?.words.get(w1_word)?;
            template_1.replace("{0}", word_1)
        } else {
            template_1.clone()
        };

        if let Some(conj_idx) = conjunction {
            if let Some(template_2_idx) = template_2_idx {
                let conj = self.conjunctions.get(conj_idx)?;
                if conj.len() > 1 || conj.chars().next().map(|x| !x.is_ascii_punctuation()).unwrap_or(false) {
                    formatted.push('\n');
                }

                formatted.push_str(conj);
                formatted.push(' ');

                let template_2 = self.templates.get(template_2_idx)?;
                if template_2.contains("{0}") && word_2_idx.is_none() {
                    return None;
                }

                let append = if let Some((w2_list, w2_word)) = word_2_idx {
                    let word_2 = self.words.get(w2_list)?.words.get(w2_word)?;
                    template_2.replace("{0}", word_2)
                } else {
                    template_2.clone()
                };

                formatted.push_str(&append);
            }
        }

        Some(formatted)
    }
}
